using System.Text.Json;
using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;

namespace ArcaneVault.Desktop;

/// <summary>Optional, locally cached Scryfall Oracle-card catalog; distinct from owned collection.</summary>
internal sealed class ScryfallCatalog
{
    private readonly string dbPath;
    
    private readonly HttpClient http;
    private readonly SemaphoreSlim gate = new(1, 1);
    private volatile string phase = "Not downloaded";
    private volatile string error = "";
    private long imported;
    private bool busy;
    private string? updatedAt;
    private int total;

    public ScryfallCatalog(HttpClient client)
    {
        // Bulk downloads need a longer timeout than the ordinary card lookup service.
        // Keep a separate client so quick Scryfall lookups retain their timeout.
        http = new HttpClient { Timeout = TimeSpan.FromHours(2) };
        http.DefaultRequestHeaders.UserAgent.Clear();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ArcaneVault/4.8.3 (+https://github.com/ArcaneVault; personal collection manager)");
        http.DefaultRequestHeaders.Accept.Clear();
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json;q=0.9, */*;q=0.8");
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArcaneVault");
        Directory.CreateDirectory(dir);
        dbPath = Path.Combine(dir, "scryfall-catalog.sqlite");
        using var db = Connect();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
          CREATE TABLE IF NOT EXISTS cards(
            name TEXT PRIMARY KEY COLLATE NOCASE,
            type TEXT NOT NULL, oracle TEXT NOT NULL, rarity TEXT NOT NULL,
            mv REAL NOT NULL, colors TEXT NOT NULL, image TEXT NOT NULL,
            uri TEXT NOT NULL, legalities TEXT NOT NULL, layout TEXT NOT NULL,
            setcode TEXT NOT NULL
          );
          CREATE INDEX IF NOT EXISTS idx_catalog_type ON cards(type);
          CREATE INDEX IF NOT EXISTS idx_catalog_rarity ON cards(rarity);
          CREATE TABLE IF NOT EXISTS variants(
            print_id TEXT PRIMARY KEY, name TEXT NOT NULL COLLATE NOCASE, setcode TEXT NOT NULL,
            setname TEXT NOT NULL, collector TEXT NOT NULL, rarity TEXT NOT NULL,
            image TEXT NOT NULL, image_back TEXT NOT NULL, art_id TEXT NOT NULL,
            games TEXT NOT NULL, arena INTEGER NOT NULL, digital INTEGER NOT NULL,
            promo INTEGER NOT NULL, full_art INTEGER NOT NULL, border TEXT NOT NULL,
            release TEXT NOT NULL, url TEXT NOT NULL
          );
          CREATE INDEX IF NOT EXISTS idx_variants_name ON variants(name COLLATE NOCASE);
          CREATE INDEX IF NOT EXISTS idx_variants_set_name ON variants(setcode,name);
          CREATE TABLE IF NOT EXISTS ownership(name TEXT PRIMARY KEY COLLATE NOCASE, qty INTEGER NOT NULL);
          CREATE TABLE IF NOT EXISTS state(key TEXT PRIMARY KEY, value TEXT NOT NULL);
          """;
        cmd.ExecuteNonQuery();
        using (var upgrade = db.CreateCommand())
        {
            upgrade.CommandText = "ALTER TABLE cards ADD COLUMN arena INTEGER NOT NULL DEFAULT 0";
            try { upgrade.ExecuteNonQuery(); } catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase)) { }
        }
        using var count = db.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM cards";
        total = Convert.ToInt32(count.ExecuteScalar());
        using var date = db.CreateCommand();
        date.CommandText = "SELECT value FROM state WHERE key='updated'";
        updatedAt = date.ExecuteScalar()?.ToString();
        phase = total > 0 ? "Ready" : "Not downloaded";
    }

    private SqliteConnection Connect()
    {
        var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        c.Open();
        return c;
    }

    public object Status() => new { busy, phase, imported, total, updatedAt, error, source = "Scryfall Default Cards (all printings, English-first)", ownershipSource = "Arena CSV", hasPrintings = VariantCount() > 0 };

    private long VariantCount()
    {
        using var db = Connect(); using var c = db.CreateCommand();
        c.CommandText = "SELECT COUNT(*) FROM variants";
        return Convert.ToInt64(c.ExecuteScalar());
    }

    public bool Start()
    {
        lock (this)
        {
            if (busy) return false;
            busy = true; error = ""; imported = 0;
        }
        _ = Task.Run(DownloadAndIndexAsync);
        return true;
    }

    private static async Task EnsureScryfallSuccessAsync(HttpResponseMessage response, string stage)
    {
        if (response.IsSuccessStatusCode) return;
        // Capture a short response body for diagnosable failures; do not log credentials.
        string body = await response.Content.ReadAsStringAsync();
        if (body.Length > 350) body = body[..350];
        throw new HttpRequestException($"{stage} failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). " +
            (response.StatusCode == System.Net.HttpStatusCode.BadRequest || response.StatusCode == System.Net.HttpStatusCode.Forbidden
              ? "Scryfall may be filtering the request; retry later or check VPN/proxy settings. " : "") + body);
    }

    private async Task DownloadAndIndexAsync()
    {
        string temp = dbPath + ".prints.download";
        try
        {
            phase = "Discovering Scryfall printing dataset";
            // The bulk-data manifest is the documented discovery mechanism.
            // A 400 from the Scryfall API must not be masked as a download failure.
            using var infoResponse = await http.GetAsync("https://api.scryfall.com/bulk-data",
                HttpCompletionOption.ResponseHeadersRead);
            await EnsureScryfallSuccessAsync(infoResponse, "Scryfall bulk catalog discovery");
            using var info = JsonDocument.Parse(await infoResponse.Content.ReadAsStringAsync());
            var root = info.RootElement;
            if (!root.TryGetProperty("data", out var datasets) || datasets.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Scryfall bulk catalog did not contain a datasets list.");
            JsonElement dataset = default;
            foreach (var entry in datasets.EnumerateArray())
            {
                if (entry.TryGetProperty("type", out var type) && type.GetString() == "default_cards")
                { dataset = entry; break; }
            }
            if (dataset.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Scryfall's bulk catalog does not list the default_cards dataset.");
            string? urlText = dataset.TryGetProperty("jsonl_download_uri", out var jl) ? jl.GetString() : null;
            if (string.IsNullOrWhiteSpace(urlText) && dataset.TryGetProperty("download_uri", out var u))
                urlText = u.GetString();
            if (!Uri.TryCreate(urlText, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps ||
                !(url.Host.Equals("data.scryfall.io", StringComparison.OrdinalIgnoreCase) || url.Host.Equals("data.scryfall.com", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Scryfall returned an unexpected download host.");
            phase = "Downloading all printings (may be several hundred MB)";
            using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                await EnsureScryfallSuccessAsync(response, "Scryfall catalog file download");
                if (response.Content.Headers.ContentLength > 2_000_000_000L) throw new InvalidDataException("Unexpected catalog file size.");
                await using var destination = File.Create(temp);
                await response.Content.CopyToAsync(destination);
            }
            phase = "Indexing card names and artwork variants";
            await gate.WaitAsync();
            try
            {
                using var db = Connect();
                using (var setup = db.CreateCommand())
                {
                    setup.CommandText = """
                        DROP TABLE IF EXISTS cards_stage;
                        DROP TABLE IF EXISTS variants_stage;
                        CREATE TABLE cards_stage(name TEXT PRIMARY KEY COLLATE NOCASE, type TEXT NOT NULL,
                          oracle TEXT NOT NULL, rarity TEXT NOT NULL, mv REAL NOT NULL, colors TEXT NOT NULL,
                          image TEXT NOT NULL, uri TEXT NOT NULL, legalities TEXT NOT NULL, layout TEXT NOT NULL,
                          setcode TEXT NOT NULL, arena INTEGER NOT NULL DEFAULT 0);
                        CREATE TABLE variants_stage(print_id TEXT PRIMARY KEY, name TEXT NOT NULL COLLATE NOCASE,
                          setcode TEXT NOT NULL, setname TEXT NOT NULL, collector TEXT NOT NULL,
                          rarity TEXT NOT NULL, image TEXT NOT NULL, image_back TEXT NOT NULL,
                          art_id TEXT NOT NULL, games TEXT NOT NULL, arena INTEGER NOT NULL,
                          digital INTEGER NOT NULL, promo INTEGER NOT NULL, full_art INTEGER NOT NULL,
                          border TEXT NOT NULL, release TEXT NOT NULL, url TEXT NOT NULL);
                        """;
                    setup.ExecuteNonQuery();
                }
                using var tr = db.BeginTransaction();
                using var card = db.CreateCommand(); card.Transaction = tr;
                card.CommandText = """
                    INSERT INTO cards_stage(name,type,oracle,rarity,mv,colors,image,uri,legalities,layout,setcode,arena)
                    VALUES($n,$t,$o,$r,$mv,$c,$i,$u,$l,$ly,$s,$a)
                    ON CONFLICT(name) DO UPDATE SET arena=MAX(cards_stage.arena,excluded.arena),
                    image=CASE WHEN excluded.arena=1 AND cards_stage.arena=0 THEN excluded.image ELSE cards_stage.image END
                    """;
                foreach (var n in new[]{"$n","$t","$o","$r","$mv","$c","$i","$u","$l","$ly","$s","$a"}) card.Parameters.Add(new SqliteParameter(n, ""));
                using var art = db.CreateCommand(); art.Transaction = tr;
                art.CommandText = """
                    INSERT OR REPLACE INTO variants_stage(print_id,name,setcode,setname,collector,rarity,image,image_back,art_id,games,arena,digital,promo,full_art,border,release,url)
                    VALUES($id,$n,$s,$sn,$cn,$r,$i,$ib,$ai,$g,$a,$d,$p,$f,$b,$rel,$u)
                    """;
                foreach (var n in new[]{"$id","$n","$s","$sn","$cn","$r","$i","$ib","$ai","$g","$a","$d","$p","$f","$b","$rel","$u"}) art.Parameters.Add(new SqliteParameter(n, ""));
                await using var fs = File.OpenRead(temp);
                bool gzipped = fs.ReadByte()==0x1f && fs.ReadByte()==0x8b; fs.Position=0;
                await using Stream stream = gzipped ? new GZipStream(fs, CompressionMode.Decompress) : fs;
                // Scryfall's current bulk files may be JSONL (compressed), with legacy JSON arrays as fallback.
                using var buf = new BufferedStream(stream, 65536);
                int firstByte; do { firstByte=buf.ReadByte(); } while (firstByte>=0 && char.IsWhiteSpace((char)firstByte));
                if (firstByte<0) throw new InvalidDataException("Empty catalog data.");
                // Reinsert the consumed first byte without seeking within the gzip stream.
                using var prefixed = new FirstByteStream((byte)firstByte, buf);
                if (firstByte=='[')
                {
                    await foreach (var element in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(prefixed))
                        if (element.ValueKind==JsonValueKind.Object) Insert(element);
                }
                else
                {
                    using var reader = new StreamReader(prefixed, Encoding.UTF8, false, 65536, leaveOpen:true);
                    string? line;
                    while ((line=await reader.ReadLineAsync()) is not null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        using var json = JsonDocument.Parse(line);
                        Insert(json.RootElement);
                    }
                }
                tr.Commit();
                phase = "Finalizing searchable catalog";
                using var finishTr = db.BeginTransaction();
                using var finish = db.CreateCommand(); finish.Transaction=finishTr;
                finish.CommandText = """
                    DELETE FROM cards;
                    INSERT INTO cards(name,type,oracle,rarity,mv,colors,image,uri,legalities,layout,setcode,arena)
                    SELECT name,type,oracle,rarity,mv,colors,image,uri,legalities,layout,setcode,arena FROM cards_stage;
                    DELETE FROM variants;
                    INSERT INTO variants SELECT * FROM variants_stage;
                    DROP TABLE cards_stage; DROP TABLE variants_stage;
                    INSERT OR REPLACE INTO state(key,value) VALUES('updated',$updated);
                    """;
                updatedAt=DateTimeOffset.UtcNow.ToString("O");
                finish.Parameters.AddWithValue("$updated",updatedAt);
                finish.ExecuteNonQuery(); finishTr.Commit();
                using var count = db.CreateCommand(); count.CommandText="SELECT COUNT(*) FROM cards";
                total=Convert.ToInt32(count.ExecuteScalar());
                phase="Ready";

                void Insert(JsonElement raw)
                {
                    string Get(string key) => raw.TryGetProperty(key,out var v) && v.ValueKind==JsonValueKind.String ? v.GetString() ?? "" : "";
                    bool Flag(string key) => raw.TryGetProperty(key,out var v) && v.ValueKind==JsonValueKind.True;
                    string name=Get("name"), id=Get("id");
                    if (name.Length is < 1 or > 300 || !Guid.TryParse(id,out _)) return;
                    string games=raw.TryGetProperty("games",out var g)&&g.ValueKind==JsonValueKind.Array ? string.Join(',',g.EnumerateArray().Select(x=>x.GetString())) : "";
                    bool arena=games.Split(',').Contains("arena");
                    string image="",back="",oracle=Get("oracle_text"), artId=Get("illustration_id");
                    if (raw.TryGetProperty("image_uris",out var images)&&images.ValueKind==JsonValueKind.Object && images.TryGetProperty("normal",out var nm)) image=nm.GetString()??"";
                    if (raw.TryGetProperty("card_faces",out var faces)&&faces.ValueKind==JsonValueKind.Array)
                    {
                        if (oracle.Length==0) oracle=string.Join("\n//\n",faces.EnumerateArray().Select(f=>f.TryGetProperty("oracle_text",out var ot)?ot.GetString():""));
                        foreach (var face in faces.EnumerateArray())
                        {
                            if (face.TryGetProperty("image_uris",out var fi)&&fi.TryGetProperty("normal",out var im))
                            { if (image.Length==0) image=im.GetString()??""; else if (back.Length==0 && im.GetString()!=image) back=im.GetString()??""; }
                            if (artId.Length==0 && face.TryGetProperty("illustration_id",out var aid)) artId=aid.GetString()??"";
                        }
                    }
                    string ci=raw.TryGetProperty("color_identity",out var colors)&&colors.ValueKind==JsonValueKind.Array ? string.Join("",colors.EnumerateArray().Select(x=>x.GetString())) : "";
                    double mv=raw.TryGetProperty("cmc",out var cmc)&&cmc.TryGetDouble(out var n)?n:0;
                    void Set(SqliteCommand cmd,string p,object v)=>cmd.Parameters[p].Value=v;
                    Set(card,"$n",name);Set(card,"$t",Get("type_line"));Set(card,"$o",oracle);Set(card,"$r",Get("rarity"));Set(card,"$mv",mv);Set(card,"$c",ci);
                    Set(card,"$i",image);Set(card,"$u",Get("scryfall_uri"));Set(card,"$l",raw.TryGetProperty("legalities",out var leg)?leg.GetRawText():"{}");
                    Set(card,"$ly",Get("layout"));Set(card,"$s",Get("set"));Set(card,"$a",arena?1:0);card.ExecuteNonQuery();
                    Set(art,"$id",id);Set(art,"$n",name);Set(art,"$s",Get("set"));Set(art,"$sn",Get("set_name"));Set(art,"$cn",Get("collector_number"));
                    Set(art,"$r",Get("rarity"));Set(art,"$i",image);Set(art,"$ib",back);Set(art,"$ai",artId);Set(art,"$g",games);
                    Set(art,"$a",arena?1:0);Set(art,"$d",Flag("digital")?1:0);Set(art,"$p",Flag("promo")?1:0);
                    Set(art,"$f",Flag("full_art")?1:0);Set(art,"$b",Get("border_color"));Set(art,"$rel",Get("released_at"));Set(art,"$u",Get("scryfall_uri"));art.ExecuteNonQuery();
                    imported++;
                }
            }
            finally { gate.Release(); }
        }
        catch(Exception ex) { error=ex.Message; phase="Sync failed; previous catalog preserved"; }
        finally { try { if(File.Exists(temp)) File.Delete(temp); } catch{} busy=false; }
    }

    // Preserves the first inspected byte when reading from non-seekable gzip streams.
    private sealed class FirstByteStream(byte first, Stream inner) : Stream
    {
        private bool hasFirst=true;
        public override int Read(byte[] buffer,int offset,int count)
        { if(count==0)return 0; if(hasFirst){hasFirst=false;buffer[offset]=first;return 1;}return inner.Read(buffer,offset,count); }
        public override int Read(Span<byte> buffer)
        { if(buffer.Length==0)return 0; if(hasFirst){hasFirst=false;buffer[0]=first;return 1;}return inner.Read(buffer); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)
        { if(buffer.Length>0 && hasFirst){hasFirst=false;buffer.Span[0]=first;return ValueTask.FromResult(1);}return inner.ReadAsync(buffer,cancellationToken); }
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush(){}public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }

    public object Variants(string name,string scope)
    {
        if(name.Length is <1 or >300) throw new ArgumentException("Invalid card name");
        using var db=Connect();using var cmd=db.CreateCommand();
        cmd.CommandText="""
            SELECT print_id,setcode,setname,collector,rarity,image,image_back,art_id,games,
                   arena,digital,promo,full_art,border,release,url
            FROM variants WHERE name=$name AND ($scope!='arena' OR arena=1)
            ORDER BY arena DESC, release DESC, setcode, collector LIMIT 400
            """;
        cmd.Parameters.AddWithValue("$name",name);cmd.Parameters.AddWithValue("$scope",scope);
        var rows=new List<object>();using var reader=cmd.ExecuteReader();
        while(reader.Read()) rows.Add(new { id=reader.GetString(0),set=reader.GetString(1),setName=reader.GetString(2),collectorNumber=reader.GetString(3),rarity=reader.GetString(4),image=reader.GetString(5),imageBack=reader.GetString(6),illustrationId=reader.GetString(7),games=reader.GetString(8).Split(',',StringSplitOptions.RemoveEmptyEntries),arena=reader.GetInt32(9)>0,digital=reader.GetInt32(10)>0,promo=reader.GetInt32(11)>0,fullArt=reader.GetInt32(12)>0,border=reader.GetString(13),released=reader.GetString(14),url=reader.GetString(15) });
        return new {name,scope,count=rows.Count,variants=rows,source="Scryfall printings (not a complete list of MTG Arena cosmetic card styles)"};
    }

    // Use request-local temporary ownership for paper inventories. Never alter Arena ownership.
    private static void PreparePhysicalOwnership(SqliteConnection db, JsonElement? physicalCards)
    {
        if (!physicalCards.HasValue) return;
        var array = physicalCards.Value;
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 150_000)
            throw new ArgumentException("Expected an array of physical cards.");
        using var create = db.CreateCommand();
        create.CommandText = "CREATE TEMP TABLE physical_ownership(name TEXT PRIMARY KEY COLLATE NOCASE, qty INTEGER NOT NULL)";
        create.ExecuteNonQuery();
        using var insert = db.CreateCommand();
        insert.CommandText = "INSERT INTO physical_ownership(name,qty) VALUES($name,$qty) ON CONFLICT(name) DO UPDATE SET qty=qty+excluded.qty";
        insert.Parameters.Add("$name", SqliteType.Text);
        insert.Parameters.Add("$qty", SqliteType.Integer);
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("name", out var name) ||
                name.ValueKind != JsonValueKind.String || !item.TryGetProperty("qty", out var count) ||
                !count.TryGetInt32(out int qty) || qty <= 0 || qty > 99999) continue;
            var cardName = (name.GetString() ?? "").Trim();
            if (cardName.Length is < 1 or > 300) continue;
            insert.Parameters["$name"].Value = cardName;
            insert.Parameters["$qty"].Value = qty;
            insert.ExecuteNonQuery();
        }
    }

    // Set completion counts distinct card names, not printing count. The CSV tracks owned cards by name,
    // so it cannot prove ownership of a specific set printing or MTGA cosmetic treatment.
    public object SetProgress(string arenaFilter = "any", string query = "", JsonElement? physicalCards = null)
    {
        using var db = Connect();
        PreparePhysicalOwnership(db, physicalCards);
        string ownershipTable = physicalCards.HasValue ? "physical_ownership" : "ownership";
        using var cmd = db.CreateCommand();
        cmd.CommandText = $"""
            WITH distinct_cards AS (
              SELECT DISTINCT v.setcode, v.name
              FROM variants v
              WHERE ($arena != 'arena' OR v.arena=1)
            ), per_set AS (
              SELECT d.setcode, COUNT(*) AS total,
                SUM(CASE WHEN COALESCE(o.qty,0)>0 THEN 1 ELSE 0 END) AS owned
              FROM distinct_cards d LEFT JOIN {ownershipTable} o ON o.name=d.name
              GROUP BY d.setcode
            ), names AS (
              SELECT setcode, MAX(setname) AS setname FROM variants GROUP BY setcode
            )
            SELECT p.setcode,n.setname,p.total,p.owned
            FROM per_set p JOIN names n ON n.setcode=p.setcode
            WHERE ($q = '' OR p.setcode LIKE $search OR n.setname LIKE $search)
            ORDER BY n.setname COLLATE NOCASE
            """;
        string q = (query ?? "").Trim(); if (q.Length>100) q=q[..100];
        cmd.Parameters.AddWithValue("$arena",arenaFilter=="arena"?"arena":"any");
        cmd.Parameters.AddWithValue("$q",q);
        cmd.Parameters.AddWithValue("$search","%"+q+"%");
        var result = new List<object>(); int totalCards=0, ownedCards=0;
        using var reader=cmd.ExecuteReader();
        while(reader.Read())
        {
            int count=reader.GetInt32(2), owned=reader.GetInt32(3);
            totalCards+=count;ownedCards+=owned;
            result.Add(new { code=reader.GetString(0), name=reader.GetString(1), total=count,owned,missing=count-owned,percentage=count==0?0:Math.Round(100.0*owned/count,1) });
        }
        return new { sets=result,count=result.Count, totalCards,ownedCards,source=physicalCards.HasValue ? "Scryfall card-name completion; physical collection" : "Scryfall card-name completion; ownership from imported Arena CSV",note="Owned means you own the card name, not necessarily its printing in this set. Tokens, promo sets and supplemental sets may appear. Arena-listed means a Scryfall printing is marked as played on Arena." };
    }

    public object SetCards(string setcode, string arenaFilter, string ownershipFilter, int page, int size, JsonElement? physicalCards = null)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(setcode??"", "^[a-zA-Z0-9]{2,8}$")) throw new ArgumentException("Invalid set code");
        page=Math.Clamp(page,0,10000);size=Math.Clamp(size,1,100);
        if (ownershipFilter is not ("owned" or "missing" or "all")) ownershipFilter="all";
        using var db=Connect();PreparePhysicalOwnership(db, physicalCards);
        string ownershipTable=physicalCards.HasValue?"physical_ownership":"ownership";
        using var cmd=db.CreateCommand();
        var filter=ownershipFilter=="owned"?" AND COALESCE(o.qty,0)>0":ownershipFilter=="missing"?" AND COALESCE(o.qty,0)=0":"";
        string distinctSql="SELECT DISTINCT name FROM variants WHERE setcode=$set AND ($arena!='arena' OR arena=1)";
        cmd.CommandText=$"""
          WITH setcards AS ({distinctSql})
          SELECT sc.name,COALESCE(o.qty,0),COALESCE(v.image,c.image,''),COALESCE(c.type,''),COALESCE(c.rarity,''),COALESCE(c.uri,'')
          FROM setcards sc
          LEFT JOIN {ownershipTable} o ON o.name=sc.name
          LEFT JOIN cards c ON c.name=sc.name
          LEFT JOIN variants v ON v.print_id=(SELECT print_id FROM variants WHERE setcode=$set AND name=sc.name AND ($arena!='arena' OR arena=1) ORDER BY release DESC LIMIT 1)
          WHERE 1=1 {filter}
          ORDER BY sc.name COLLATE NOCASE LIMIT $limit OFFSET $offset
          """;
        cmd.Parameters.AddWithValue("$set",setcode!.ToLowerInvariant());cmd.Parameters.AddWithValue("$arena",arenaFilter=="arena"?"arena":"any");
        cmd.Parameters.AddWithValue("$limit",size);cmd.Parameters.AddWithValue("$offset",page*size);
        var rows=new List<object>();using var reader=cmd.ExecuteReader();
        while(reader.Read())rows.Add(new {name=reader.GetString(0),qty=reader.GetInt32(1),image=reader.GetString(2),type=reader.GetString(3),rarity=reader.GetString(4),url=reader.GetString(5)});
        return new { set=setcode, page,size,cards=rows,hasMore=rows.Count==size };
    }

    public void UpdateOwnership(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 150_000) throw new ArgumentException("Expected an array of owned cards.");
        gate.Wait();
        try
        {
            using var db = Connect();
            using var transaction = db.BeginTransaction();
            using var del = db.CreateCommand(); del.Transaction = transaction; del.CommandText = "DELETE FROM ownership"; del.ExecuteNonQuery();
            using var cmd = db.CreateCommand(); cmd.Transaction = transaction;
            cmd.CommandText = "INSERT OR REPLACE INTO ownership(name,qty) VALUES($n,$q)";
            cmd.Parameters.Add("$n", SqliteType.Text); cmd.Parameters.Add("$q", SqliteType.Integer);
            foreach (var card in array.EnumerateArray())
            {
                if (!card.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String) continue;
                string n = name.GetString() ?? "";
                int q = card.TryGetProperty("qty", out var qty) && qty.TryGetInt32(out int value) ? value : 0;
                if (n.Length is < 1 or > 300 || q <= 0) continue;
                cmd.Parameters["$n"].Value = n; cmd.Parameters["$q"].Value = q; cmd.ExecuteNonQuery();
            }
            transaction.Commit();
        }
        finally { gate.Release(); }
    }

    public object Search(string scope, string? q, string? type, string? rarity, string? colors, string? role, string? sort, int page, int size, string arenaFilter = "any")
    {
        using var db = Connect();
        page = Math.Clamp(page, 0, 100_000); size = Math.Clamp(size, 1, 250);
        var where = new List<string>();
        if (arenaFilter == "arena") where.Add("c.arena=1");
        else if (arenaFilter == "paper") where.Add("c.arena=0");
        if (scope == "unowned") where.Add("COALESCE(ownership.qty,0)=0");
        if (scope == "owned") where.Add("COALESCE(ownership.qty,0)>0");
        if (!string.IsNullOrWhiteSpace(q)) where.Add("(c.name LIKE $q OR c.oracle LIKE $q OR c.type LIKE $q OR c.setcode LIKE $q)");
        if (!string.IsNullOrWhiteSpace(type)) where.Add("c.type LIKE $type");
        if (!string.IsNullOrWhiteSpace(rarity)) where.Add("c.rarity=$rarity");
        if (!string.IsNullOrWhiteSpace(colors))
        {
            if (colors == "C") where.Add("c.colors=''");
            else if (colors == "B+") where.Add("c.colors LIKE '%B%'");
            else if (colors == "WU") where.Add("c.colors NOT GLOB '*[BRG]*'");
            else where.Add("c.colors=$colors");
        }
        // Heuristics are discovery aids, not rules verdicts.
        string? hint = role switch {
            "Ramp" => "%add %mana%", "Removal" => "%destroy target%", "Board Wipe" => "%destroy all%",
            "Discard" => "%discard%", "Counterspell" => "%counter target%", "Card Draw" => "%draw %card%",
            "Card Selection" => "%scry%", "Lifegain" => "%gain %life%", "Tutor" => "%search your library%",
            "Stax" => "%can't cast%", "Haste" => "%haste%", "Protection" => "%hexproof%",
            "Recursion" => "%from your graveyard%", "Sacrifice" => "%sacrifice%", "Tokens" => "%create %token%",
            "Graveyard Hate" => "%exile %graveyard%", "Finisher" => "%loses the game%", "Equipment" => "%Equipment%",
            _ => null
        };
        if (hint != null) where.Add(role == "Equipment" ? "c.type LIKE $role" : "c.oracle LIKE $role");
        string filter = where.Count == 0 ? "" : " WHERE " + string.Join(" AND ", where);
        string from = " FROM cards c LEFT JOIN ownership ON ownership.name=c.name";
        using var count = db.CreateCommand();
        count.CommandText = "SELECT COUNT(*)" + from + filter;
        SetParams(count);
        int found = Convert.ToInt32(count.ExecuteScalar());
        string order = sort switch {
            "qty" => "COALESCE(ownership.qty,0) DESC, c.name COLLATE NOCASE",
            "mv" => "c.mv ASC, c.name COLLATE NOCASE",
            "rarity" => "CASE c.rarity WHEN 'mythic' THEN 4 WHEN 'rare' THEN 3 WHEN 'uncommon' THEN 2 ELSE 1 END DESC, c.name COLLATE NOCASE",
            _ => "c.name COLLATE NOCASE"
        };
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT c.name,c.type,c.oracle,c.rarity,c.mv,c.colors,c.image,c.uri,c.legalities,c.layout,c.setcode,COALESCE(ownership.qty,0),c.arena" + from + filter + " ORDER BY " + order + " LIMIT $size OFFSET $offset";
        SetParams(cmd);
        cmd.Parameters.AddWithValue("$size", size); cmd.Parameters.AddWithValue("$offset", page * size);
        var list = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(0);
            var ci = reader.GetString(5);
            var legality = JsonSerializer.Deserialize<Dictionary<string,string>>(reader.GetString(8)) ?? new();
            list.Add(new { name, qty = reader.GetInt32(11), sets = new[] { reader.GetString(10) }, tags = Array.Empty<string>(), typeHint = "",
                meta = new { type = reader.GetString(1), oracle = reader.GetString(2), rarity = reader.GetString(3), mv = reader.GetDouble(4),
                    colors = ci.Select(x => x.ToString()).ToArray(), image = reader.GetString(6), url = reader.GetString(7), legalities = legality,
                    layout = reader.GetString(9), set_ = reader.GetString(10), arena = reader.GetInt32(12)>0 }
            });
        }
        return new { total = found, page, size, cards = list };

        void SetParams(SqliteCommand c)
        {
            if (!string.IsNullOrWhiteSpace(q)) c.Parameters.AddWithValue("$q", "%" + q.Trim()[..Math.Min(q.Trim().Length, 160)] + "%");
            if (!string.IsNullOrWhiteSpace(type)) c.Parameters.AddWithValue("$type", "%" + type + "%");
            if (!string.IsNullOrWhiteSpace(rarity)) c.Parameters.AddWithValue("$rarity", rarity);
            // SQL only contains $colors in the exact-color branch. Previously this
            // parameter was omitted, making any exact color filter throw SQLite
            // error 1 ("Insufficient parameters supplied") and return HTTP 500.
            if (!string.IsNullOrWhiteSpace(colors) && colors is not ("C" or "B+" or "WU"))
                c.Parameters.AddWithValue("$colors", colors);
            if (hint != null) c.Parameters.AddWithValue("$role", hint);
        }
    }
}
