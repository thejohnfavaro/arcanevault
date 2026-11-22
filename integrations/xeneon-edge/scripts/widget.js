"use strict";
/*
 * Arcane Vault XENEON EDGE: read-only file watcher for the existing Stream Deck
 * compatibility snapshot. Only local visual preferences are persisted.
 * No collection writes, local web servers or elevated OS permissions.
 */
(function () {
  const SNAPSHOT_PATH = "{Runtime}/AppData/Local/ArcaneVault/streamdeck.json";
  const selectionKey = "arcanevault.edge.deckId.v1";
  const autoSwitchKey = "arcanevault.edge.autoSwitchOnMatch.v1";
  const $ = id => document.getElementById(id);
  // Chromium/iCUE touch zoom guard: CSS and the viewport are primary; these
  // cancel browser-specific gesture zoom and trackpad Ctrl+wheel fallback.
  // One-finger scrolling and normal button taps are unaffected.
  document.addEventListener("gesturestart", event => event.preventDefault(), {passive:false});
  document.addEventListener("gesturechange", event => event.preventDefault(), {passive:false});
  document.addEventListener("gestureend", event => event.preventDefault(), {passive:false});
  document.addEventListener("touchmove", event => {
    if (event.touches && event.touches.length > 1) event.preventDefault();
  }, {passive:false});
  document.addEventListener("wheel", event => {
    if (event.ctrlKey) event.preventDefault();
  }, {passive:false});
  let lastSnapshot = null;
  let selectedId = "";
  let autoSwitchOnMatch = true;
  let currentIndex = 0;
  let listening = false;
  let retryImage = false;
  let lastSnapshotSignal = 0;
  let watchStartedAt = 0;
  let lastWatchRetry = 0;

  const layoutKey = "arcanevault.edge.layout.v1";
  const setKey = "arcanevault.edge.setCode.v1";
  const layoutDefaults = {history:true, latest:true, collection:true, sets:true};
  let layout = {...layoutDefaults};
  let selectedSetCode = "";
  let setListIdentity = "";
  try {
    selectedId = localStorage.getItem(selectionKey) || "";
    autoSwitchOnMatch = localStorage.getItem(autoSwitchKey) !== "false";
    selectedSetCode = localStorage.getItem(setKey) || "";
    const saved = JSON.parse(localStorage.getItem(layoutKey) || "{}");
    for (const key of Object.keys(layoutDefaults))
      if (typeof saved[key] === "boolean") layout[key] = saved[key];
  } catch (_) {}
  $("autoSwitchDeck").checked = autoSwitchOnMatch;

  function applyLayout() {
    for (const [key, element] of Object.entries({
      history:"historyPanel", latest:"latestPanel",
      collection:"collectionPanel", sets:"setPanel"
    })) {
      $(element).hidden = !layout[key];
      $("show" + key[0].toUpperCase() + key.slice(1)).checked = layout[key];
    }
    $("dashboardShell").classList.toggle("no-history", !layout.history);
    $("dashboardShell").classList.toggle("no-side",
      !layout.latest && !layout.collection && !layout.sets);
  }

  function setLayout(key, enabled) {
    layout[key] = enabled;
    try { localStorage.setItem(layoutKey, JSON.stringify(layout)); } catch (_) {}
    applyLayout();
  }

  function currentSetChoices() {
    const summary = lastSnapshot?.setProgress;
    return Array.isArray(summary?.sets) ?
      summary.sets.filter(x => x && typeof x.code === "string" &&
        Number(x.total) > 0 && Number(x.owned) > 0).slice(0,2000) : [];
  }

  function changeSet(direction) {
    const codes = ["", ...currentSetChoices().map(x => x.code)];
    let index = codes.findIndex(x => x.toLowerCase() === selectedSetCode.toLowerCase());
    if (index < 0) index = 0;
    selectedSetCode = codes[(index + direction + codes.length) % codes.length];
    try { localStorage.setItem(setKey, selectedSetCode); } catch (_) {}
    if (lastSnapshot) renderSetProgress(lastSnapshot);
  }


  function status(message, alert) {
    const node = $("status");
    node.textContent = message;
    node.classList.toggle("alert", !!alert);
  }

  function artworkUrl(raw) {
    if (typeof raw !== "string" || !raw) return "";
    try {
      const url = new URL(raw);
      if (url.protocol !== "https:" || url.hostname !== "cards.scryfall.io" ||
          (url.port && url.port !== "443") || url.username || url.password)
        return "";
      const parts = url.pathname.split("/");
      if (parts.length > 3 && ["normal","small","large","png","border_crop"].includes(parts[1])) {
        const wasPng = parts[1] === "png";
        parts[1] = "art_crop";
        if (wasPng) parts[parts.length-1] = parts[parts.length-1].replace(/\.png$/i, ".jpg");
        url.pathname = parts.join("/");
      }
      return url.href;
    } catch (_) { return ""; }
  }

  function renderMatchHistory(deck) {
    const events = Array.isArray(deck.matchHistory) ?
      deck.matchHistory.filter(x => x && (x.result === "win" || x.result === "loss") &&
        typeof x.playedUtc === "string" && !Number.isNaN(Date.parse(x.playedUtc))).slice(0,20) : [];
    // The native snapshot orders newest first; the visual strip shows oldest to newest.
    const chronological = events.slice().reverse();
    const strip = $("matchStrip");
    strip.replaceChildren();
    for (let i = 0; i < 20; i++) {
      const block = document.createElement("span");
      const item = chronological[i - Math.max(0,20-chronological.length)];
      block.className = "match-result " + (item ? item.result : "blank");
      block.title = item ? item.result.toUpperCase() + " · " +
        new Date(item.playedUtc).toLocaleDateString() : "No match";
      strip.appendChild(block);
    }
    $("historyCoverage").textContent = events.length ?
      "Latest " + events.length + " confirmed matches" : "No verified match history";
    const path = $("trendPath");
    if (chronological.length < 2) {
      path.setAttribute("d", "");
      $("trendDescription").textContent = "Trend appears after two confirmed games";
      return;
    }
    const rate = chronological.map((_, i) => {
      const start = Math.max(0,i-9);
      const recent = chronological.slice(start,i+1);
      return 100 * recent.filter(m => m.result === "win").length / recent.length;
    });
    path.setAttribute("d", rate.map((r,i) =>
      (i === 0 ? "M " : "L ") +
      (10 + 280 * i / (rate.length-1)).toFixed(1) + " " +
      (62 - r * .52).toFixed(1)).join(" "));
    $("trendDescription").textContent = "Rolling win rate · last 10 games";
  }

  function renderSetProgress(snapshot) {
    const summary = snapshot.setProgress;
    const rows = $("setRows");
    rows.replaceChildren();
    const select = $("setSelect");
    if (!summary || !Number.isFinite(Number(summary.totalSlots))) {
      $("setOverall").textContent = "—";
      $("setDescription").textContent = "Verified set progress not yet available";
      if (select.options.length !== 1) {
        select.replaceChildren(new Option("Top owned sets", ""));
      }
      setListIdentity = "";
      return;
    }
    const total = Math.max(0, Number(summary.totalSlots) || 0);
    const owned = Math.max(0, Number(summary.ownedSlots) || 0);
    const choices = currentSetChoices();
    // Options are rebuilt only when the catalog's set roster changes.
    const identity = choices.map(x => x.code + ":" + x.owned + ":" + x.total).join("|");
    if (identity !== setListIdentity) {
      setListIdentity = identity;
      select.replaceChildren(new Option("Top owned sets", ""));
      for (const set of choices) select.add(new Option(
        String(set.name || set.code) + " (" + String(set.code).toUpperCase() + ")",
        String(set.code)));
    }
    const selected = selectedSetCode ?
      choices.find(x => x.code.toLowerCase() === selectedSetCode.toLowerCase()) : null;
    if (selectedSetCode && !selected) {
      // Clear selections that are no longer eligible (including sets with zero progress).
      selectedSetCode = "";
      try { localStorage.removeItem(setKey); } catch (_) {}
    }
    select.value = selected ? selected.code : "";
    if (selected) {
      const num = Math.max(0, Number(selected.owned) || 0);
      const denom = Math.max(1, Number(selected.total) || 0);
      $("setOverall").textContent = num.toLocaleString() + " / " + denom.toLocaleString();
      $("setDescription").textContent =
        (100 * num / denom).toFixed(1) + "% · " + String(selected.name || selected.code);
    } else {
      $("setOverall").textContent = owned.toLocaleString() + " / " + total.toLocaleString();
      $("setDescription").textContent = total ?
        (100 * owned / total).toFixed(1) + "% of catalog paper collector slots" :
        "No verified paper collector slots";
    }
    const visible = selected ? [selected] :
      (Array.isArray(summary.topSets) ? summary.topSets : []).slice(0,4);
    for (const set of visible) {
      if (!set || typeof set !== "object") continue;
      const count = Number(set.owned) || 0, maximum = Number(set.total) || 0;
      if (maximum <= 0) continue;
      const item = document.createElement("div");
      item.className = "set-row" + (selected ? " selected" : "");
      const name = document.createElement("span");
      name.className = "set-name";
      name.textContent = String(set.name || set.code || "Unknown set");
      const qty = document.createElement("span");
      qty.className = "set-number";
      qty.textContent = count + " / " + maximum;
      const track = document.createElement("div");
      track.className = "set-track";
      const fill = document.createElement("div");
      fill.className = "set-fill";
      fill.style.width = Math.max(0, Math.min(100, 100 * count / maximum)) + "%";
      track.appendChild(fill);
      item.append(name, qty, track);
      rows.appendChild(item);
    }
    // Older Windows snapshots provide topSets but not the expanded selectable
    // roster. The default view remains functional without invented options.
    select.disabled = !choices.length;
    $("previousSet").disabled = !choices.length;
    $("nextSet").disabled = !choices.length;
  }

  function renderSeason(snapshot) {
    const season=snapshot?.season;
    const end=season && typeof season.endsUtc==="string" ?
      Date.parse(season.endsUtc) : NaN;
    const now=Date.now();
    if (!Number.isFinite(end) || end<=now || end>now+65*24*3600*1000) {
      $("seasonCountdown").textContent="Awaiting official date";
      $("seasonPrediction").textContent="No confirmed future season schedule.";
      $("seasonChecked").textContent="Windows checks Wizards automatically every 12 hours while running";
      return;
    }
    const remaining=end-now;
    const days=Math.floor(remaining/86400000);
    const hours=Math.floor(remaining%86400000/3600000);
    const minutes=Math.floor(remaining%3600000/60000);
    $("seasonCountdown").textContent=days+"d "+hours+"h "+minutes+"m · "+
      new Date(end).toLocaleString();
    const constructed=typeof snapshot.predictedConstructed==="string" &&
      snapshot.predictedConstructed.length<45 ? snapshot.predictedConstructed : null;
    const limited=typeof snapshot.predictedLimited==="string" &&
      snapshot.predictedLimited.length<45 ? snapshot.predictedLimited : null;
    $("seasonPrediction").textContent=(constructed||limited) ?
      "If ranks stay the same: Constructed → "+(constructed||"not verified")+
      " · Limited → "+(limited||"not verified") :
      "Post-reset rank prediction unavailable for these tiers.";
    const checked=typeof season.checkedUtc==="string" ? Date.parse(season.checkedUtc) : NaN;
    $("seasonChecked").textContent=Number.isFinite(checked) ?
      "Wizards schedule checked "+new Date(checked).toLocaleString() :
      "Wizards schedule · waiting for verified refresh";
  }

  function renderRanks(snapshot) {
    const rank = snapshot?.rank;
    // Actual MTG Arena division artwork (separate Constructed and Limited).
    // This list is fixed: never interpolate arbitrary data into file paths.
    const ranked = new Set(["bronze", "silver", "gold", "platinum", "diamond"]);
    const showRank = (lane, rawTier, rawLevel) => {
      const label = $(lane + "Rank");
      const badge = $(lane + "RankIcon");
      const tier = typeof rawTier === "string" ? rawTier.trim() : "";
      const key = tier.toLowerCase();
      const level = Number(rawLevel);
      const division = Number.isInteger(level) && level >= 1 && level <= 4 ? level : null;
      const validText = /^[a-z ]{1,32}$/i.test(tier) && key !== "invalid";
      let imageName = "";
      if (ranked.has(key) && division !== null) {
        imageName = "rank_" + lane + "_" + key + "_t" + division + ".png";
      } else if (key === "mythic" || key === "unranked") {
        imageName = "rank_" + lane + "_" + key + ".png";
      }
      // A missing division cannot safely select one of four different badges.
      label.textContent = validText ?
        tier + (ranked.has(key) && division !== null ? " " + division : "") :
        "Not captured";
      if (imageName) {
        const src = "resources/mtga-ranks/" + imageName;
        if (badge.getAttribute("src") !== src) badge.setAttribute("src", src);
        badge.hidden = false;
        badge.alt = tier + (division !== null && ranked.has(key) ? " tier " + division : "") +
          " " + lane + " Arena rank";
      } else {
        badge.hidden = true;
        badge.removeAttribute("src");
        badge.alt = "";
      }
    };
    showRank("constructed", rank?.constructedClass, rank?.constructedLevel);
    showRank("limited", rank?.limitedClass, rank?.limitedLevel);
    const asOf = rank && typeof rank.asOfUtc === "string" ?
      Date.parse(rank.asOfUtc) : NaN;
    $("rankAge").textContent = Number.isFinite(asOf) ?
      "Arena rank last observed " + new Date(asOf).toLocaleString() :
      "No verified Arena rank response captured yet";
    renderSeason(snapshot);
  }

  // The EDGE displays Arena match performance, so only Arena Brawl decks
  // belong in its selector. Tabletop Commander stays in Arcane Vault and
  // Stream Deck snapshots, but must not be interpreted as Arena Brawl.
  function arenaBrawlDecks(snapshot) {
    return Array.isArray(snapshot?.decks) ?
      snapshot.decks.filter(d => d && d.id &&
        typeof d.format === "string" &&
        d.format.trim().toLowerCase() === "arena brawl") : [];
  }

  function render(snapshot) {
    const decks = arenaBrawlDecks(snapshot);
    if (!decks.length) {
      $("deckName").textContent = "No Arena Brawl decks";
      $("commander").textContent = "Save an Arena Brawl deck in Arcane Vault";
      $("deckCount").textContent = "0 / 0";
      $("wins").textContent = "—"; $("losses").textContent = "—";
      $("rate").textContent = "—"; $("rateFill").style.width = "0%";
      $("matchStrip").replaceChildren(); $("trendPath").setAttribute("d","");
      renderSetProgress(snapshot);
      renderRanks(snapshot);
      $("art").removeAttribute("src"); $("art").classList.remove("ready");
      status("Connected · no Arena Brawl decks in this snapshot.", true);
      return;
    }
    let index = decks.findIndex(d => String(d.id) === selectedId);
    if (index < 0) index = 0;
    currentIndex = index;
    const deck = decks[index];
    selectedId = String(deck.id);
    $("deckName").textContent = String(deck.name || "Unnamed deck");
    $("commander").textContent = deck.commander ? "Commander: " + deck.commander : "No commander assigned";
    $("deckCount").textContent = (index + 1) + " / " + decks.length;
    const wins = Math.max(0, Number(deck.wins) || 0);
    const losses = Math.max(0, Number(deck.losses) || 0);
    const total = wins + losses;
    const rate = total ? Math.round(wins / total * 100) : 0;
    $("wins").textContent = String(wins);
    $("losses").textContent = String(losses);
    $("rate").textContent = total ? rate + "%" : "—";
    $("rateFill").style.width = (total ? rate : 0) + "%";
    $("recordSummary").textContent = total ? "Confirmed Arena matches" : "No associated matches";
    renderMatchHistory(deck);
    renderSetProgress(snapshot);
    renderRanks(snapshot);
    const value = snapshot.collection?.value;
    const currency = snapshot.collection?.currency || "USD";
    const numericValue = value === null || value === undefined || value === "" ? NaN : Number(value);
    let valueLabel = "—";
    if (Number.isFinite(numericValue)) {
      try { valueLabel = new Intl.NumberFormat("en-US", {style:"currency",currency,maximumFractionDigits:0})
        .format(numericValue); } catch (_) { valueLabel = numericValue.toFixed(0); }
    }
    $("collectionValue").textContent = valueLabel;
    $("valuationAge").textContent = snapshot.collection?.asOf ?
      "Valuation: " + snapshot.collection.asOf : "No recent valuation";
    const image = $("art");
    const url = artworkUrl(deck.artUrl);
    // Do not blank and re-fetch the same image on every 15s snapshot update.
    if (image.dataset.selectedDeck === selectedId && image.dataset.artUrl === url &&
        image.classList.contains("ready")) return;
    image.dataset.selectedDeck = selectedId;
    image.dataset.artUrl = url;
    retryImage = false;
    image.classList.remove("ready");
    image.onload = () => { image.classList.add("ready"); status("Arcane Vault connected · artwork loaded", false); };
    image.onerror = () => {
      if (!retryImage && typeof deck.artUrl === "string" && artworkUrl(deck.artUrl) !== deck.artUrl) {
        retryImage = true;
        image.src = deck.artUrl; // trusted Scryfall image, full-card fallback
      } else {
        image.classList.remove("ready");
        status("Arcane Vault connected · image unavailable (" +
          String(deck.artStatus || "no cached crop") + ")", true);
      }
    };
    if (url) image.src = url;
    else { image.removeAttribute("src"); status("Arcane Vault connected · " +
      String(deck.artStatus || "commander art unavailable"), true); }
  }

  function cycle(direction) {
    const decks = arenaBrawlDecks(lastSnapshot);
    if (!decks.length) return;
    currentIndex = (currentIndex + direction + decks.length) % decks.length;
    selectedId = String(decks[currentIndex].id);
    try { localStorage.setItem(selectionKey, selectedId); } catch (_) {}
    render(lastSnapshot);
  }

  // Compare only consecutive verified snapshots, never historical totals from the
  // first event after startup. A heartbeat, artwork refresh, changed deck order,
  // or removing/correcting a match without a new win/loss cannot switch decks.
  function deckMatchCount(deck, key) {
    const count = Number(deck?.[key]);
    return Number.isSafeInteger(count) && count >= 0 ? count : 0;
  }

  function mostRecentMatchTimestamp(deck) {
    if (!Array.isArray(deck.matchHistory)) return 0;
    let mostRecent = 0;
    for (const event of deck.matchHistory) {
      if (event?.result !== "win" && event?.result !== "loss") continue;
      const time = Date.parse(event.playedUtc);
      if (Number.isFinite(time) && time > mostRecent) mostRecent = time;
    }
    return mostRecent;
  }

  function newlyUpdatedDeck(previous, incoming) {
    if (!previous || !autoSwitchOnMatch) return null;
    const before = new Map(arenaBrawlDecks(previous).map(d => [String(d.id), d]));
    const updated = [];
    for (const deck of arenaBrawlDecks(incoming)) {
      const original = before.get(String(deck.id));
      if (!original) continue; // Newly discovered/synced decks aren't new match events.
      const priorWins = deckMatchCount(original, "wins");
      const priorLosses = deckMatchCount(original, "losses");
      const wins = deckMatchCount(deck, "wins");
      const losses = deckMatchCount(deck, "losses");
      // Only upward W/L movement means a newly attributed result (or an
      // explicitly corrected outcome); heartbeat and unrelated fields don't.
      if (wins <= priorWins && losses <= priorLosses) continue;
      updated.push({deck, latest:mostRecentMatchTimestamp(deck),
        delta:Math.max(0, wins - priorWins) + Math.max(0, losses - priorLosses)});
    }
    updated.sort((a,b) => b.latest - a.latest || b.delta - a.delta ||
      String(a.deck.id).localeCompare(String(b.deck.id)));
    return updated.length ? String(updated[0].deck.id) : null;
  }

  function readSnapshot(content) {
    try {
      if (typeof content !== "string" || content.length > 8 * 1024 * 1024)
        throw Error("Missing or oversized snapshot");
      const data = JSON.parse(content);
      if (!data || !Array.isArray(data.decks)) throw Error("Invalid deck list");
      const updatedId = newlyUpdatedDeck(lastSnapshot, data);
      lastSnapshot = data;
      if (updatedId) selectedId = updatedId;
      lastSnapshotSignal = Date.now();
      status("Arcane Vault connected", false);
      render(lastSnapshot);
    } catch (error) {
      // Keep the last valid data visible if native application is saving or offline.
      status("Snapshot unavailable: " + error.message + (lastSnapshot ? " · showing last data" : ""), true);
    }
  }

  function watchFile(provider) {
    try {
      provider.watchPath(SNAPSHOT_PATH);
      watchStartedAt = Date.now();
      return true;
    } catch (error) {
      status("Cannot watch Arcane Vault snapshot: " + String(error?.message || error), true);
      return false;
    }
  }

  function watcherReady() {
    if (listening) return;
    const provider = window.plugins?.Filesystemprovider;
    if (!provider || !provider.fileChanged || !provider.watchPath) {
      status("File System Provider not ready; check iCUE widget permissions.", true);
      return;
    }
    try {
      // iCUE emits fileChanged on *content changes*, not necessarily when
      // subscribing. Native v0.9.149 adds a 15-second heartbeat so freshly
      // imported widgets don't get stranded waiting for another match.
      provider.fileChanged.connect((path, content) => {
        if (String(path).toLowerCase().replace(/\\/g, "/").endsWith("/streamdeck.json"))
          readSnapshot(content);
      });
      listening = true;
      status("Waiting for Arcane Vault heartbeat (about 15 seconds)…", false);
      watchFile(provider);
    } catch (error) {
      listening = false;
      status("iCUE snapshot listener failed: " + String(error?.message || error), true);
    }
  }

  function verifyConnection() {
    // Keep the countdown advancing even if no new match or rank events arrive.
    if (lastSnapshot) renderSeason(lastSnapshot);
    if (!listening) {
      watcherReady();
      return;
    }
    // Avoid reporting "connected" when the only data came from a stale
    // cached event; provide a precise user-actionable status instead.
    const now = Date.now();
    if (lastSnapshotSignal && now - lastSnapshotSignal < 45000) return;
    if (now - watchStartedAt < 26000) return;
    status(lastSnapshot ?
      "Snapshot not updating · check Arcane Vault is open and iCUE file access." :
      "No snapshot event · check Arcane Vault is open and grant iCUE access to streamdeck.json.", true);
    if (now - lastWatchRetry < 30000) return;
    lastWatchRetry = now;
    const provider = window.plugins?.Filesystemprovider;
    if (!provider) return;
    try {
      if (typeof provider.unwatchPath === "function") provider.unwatchPath(SNAPSHOT_PATH);
      watchFile(provider);
    } catch (error) {
      status("iCUE snapshot rewatch failed: " + String(error?.message || error), true);
    }
  }

  window.icueEvents = { onICUEInitialized: watcherReady, onDataUpdated: watcherReady };
  window.pluginFilesystemproviderEvents = { onInitialized: watcherReady };
  $("prev").addEventListener("click", () => cycle(-1));
  $("next").addEventListener("click", () => cycle(1));
  $("previousSet").addEventListener("click", () => changeSet(-1));
  $("nextSet").addEventListener("click", () => changeSet(1));
  $("setSelect").addEventListener("change", event => {
    selectedSetCode = String(event.target.value || "");
    try { localStorage.setItem(setKey, selectedSetCode); } catch (_) {}
    if (lastSnapshot) renderSetProgress(lastSnapshot);
  });
  for (const key of Object.keys(layoutDefaults)) {
    $("show" + key[0].toUpperCase() + key.slice(1))
      .addEventListener("change", event => setLayout(key, !!event.target.checked));
  }
  $("resetLayout").addEventListener("click", () => {
    layout = {...layoutDefaults};
    try { localStorage.removeItem(layoutKey); } catch (_) {}
    applyLayout();
  });
  $("autoSwitchDeck").addEventListener("change", event => {
    autoSwitchOnMatch = !!event.target.checked;
    try { localStorage.setItem(autoSwitchKey, String(autoSwitchOnMatch)); } catch (_) {}
    // The newly enabled option applies only to later match changes, not any
    // historical mismatch from when auto-switch was disabled.
  });
  $("settingsToggle").addEventListener("click", () => {
    const panel = $("settingsPanel");
    panel.hidden = !panel.hidden;
    $("settingsToggle").setAttribute("aria-expanded", String(!panel.hidden));
  });
  applyLayout();
  // Re-check the file watcher if iCUE loads the provider late or the file
  // handle changes after Arcane Vault's atomic snapshot replacement.
  setInterval(verifyConnection, 5000);
  if (typeof pluginFilesystemprovider_initialized !== "undefined" && pluginFilesystemprovider_initialized)
    watcherReady();
  else if (typeof iCUE_initialized !== "undefined" && iCUE_initialized)
    watcherReady();

  // Never render synthetic deck or collection values in the released widget.
  // A missing iCUE provider remains a clearly labeled unavailable state.
}());
