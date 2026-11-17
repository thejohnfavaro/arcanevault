"use strict";
/*
 * Arcane Vault XENEON EDGE: read-only file watcher for the existing Stream Deck
 * compatibility snapshot. Only current visual deck selection is stored locally.
 * No collection writes, local web servers or elevated OS permissions.
 */
(function () {
  const SNAPSHOT_PATH = "{Runtime}/AppData/Local/ArcaneVault/streamdeck.json";
  const selectionKey = "arcanevault.edge.deckId.v1";
  const $ = id => document.getElementById(id);
  let lastSnapshot = null;
  let selectedId = "";
  let currentIndex = 0;
  let listening = false;
  let retryImage = false;

  const layoutKey = "arcanevault.edge.layout.v1";
  const setKey = "arcanevault.edge.setCode.v1";
  const layoutDefaults = {history:true, latest:true, collection:true, sets:true};
  let layout = {...layoutDefaults};
  let selectedSetCode = "";
  let setListIdentity = "";
  try {
    selectedId = localStorage.getItem(selectionKey) || "";
    selectedSetCode = localStorage.getItem(setKey) || "";
    const saved = JSON.parse(localStorage.getItem(layoutKey) || "{}");
    for (const key of Object.keys(layoutDefaults))
      if (typeof saved[key] === "boolean") layout[key] = saved[key];
  } catch (_) {}

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
        Number(x.total) > 0).slice(0,2000) : [];
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
    if (selectedSetCode && !selected && choices.length) {
      // An old selection can outlive a downloaded set catalog.
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

  function render(snapshot) {
    const decks = Array.isArray(snapshot?.decks) ? snapshot.decks.filter(d => d && d.id) : [];
    if (!decks.length) {
      $("deckName").textContent = "No saved decks";
      $("commander").textContent = "Add a deck in Arcane Vault Native";
      $("deckCount").textContent = "0 / 0";
      $("wins").textContent = "—"; $("losses").textContent = "—";
      $("rate").textContent = "—"; $("rateFill").style.width = "0%";
      $("matchStrip").replaceChildren(); $("trendPath").setAttribute("d","");
      renderSetProgress(snapshot);
      $("art").removeAttribute("src"); $("art").classList.remove("ready");
      status("Connected, but no saved decks are available.", true);
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
    // Prefer the selected deck's own confirmed history; the v0.1 global
    // recentMatch is retained for compatibility with older native snapshots.
    const ownRecent = Array.isArray(deck.matchHistory) && deck.matchHistory.length ?
      deck.matchHistory[0] : null;
    const globalRecent = snapshot.recentMatch;
    const globalMatchesDeck = globalRecent &&
      String(globalRecent.deckName || "").trim().toLowerCase() ===
      String(deck.name || "").trim().toLowerCase();
    const recent = ownRecent || (globalMatchesDeck ? globalRecent : null);
    const recentResult = recent && (recent.result === "win" || recent.result === "loss") ?
      recent.result : "";
    $("recent").textContent = recentResult ? recentResult.toUpperCase() : "—";
    $("recent").className = "feature-data latest" + (recentResult ? " " + recentResult : "");
    $("recentDescription").textContent = recentResult ?
      (ownRecent && ownRecent.playedUtc ?
       "Confirmed on " + new Date(ownRecent.playedUtc).toLocaleDateString() :
       "Latest confirmed result for selected deck") :
      "No recent match linked to this deck";
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
    const decks = Array.isArray(lastSnapshot?.decks) ? lastSnapshot.decks : [];
    if (!decks.length) return;
    currentIndex = (currentIndex + direction + decks.length) % decks.length;
    selectedId = String(decks[currentIndex].id);
    try { localStorage.setItem(selectionKey, selectedId); } catch (_) {}
    render(lastSnapshot);
  }

  function readSnapshot(content) {
    try {
      if (typeof content !== "string" || content.length > 8 * 1024 * 1024)
        throw Error("Missing or oversized snapshot");
      const data = JSON.parse(content);
      if (!data || !Array.isArray(data.decks)) throw Error("Invalid deck list");
      lastSnapshot = data;
      status("Arcane Vault connected", false);
      render(lastSnapshot);
    } catch (error) {
      // Keep the last valid data visible if native application is saving or offline.
      status("Snapshot unavailable: " + error.message + (lastSnapshot ? " · showing last data" : ""), true);
    }
  }

  function watcherReady() {
    if (listening) return;
    const provider = window.plugins?.Filesystemprovider;
    if (!provider || !provider.fileChanged || !provider.watchPath) {
      status("iCUE File System Provider is unavailable.", true);
      return;
    }
    listening = true;
    provider.fileChanged.connect((path, content) => {
      if (String(path).toLowerCase().endsWith("streamdeck.json"))
        readSnapshot(content);
    });
    // The provider may deliver a snapshot synchronously. Set the waiting
    // status first so it cannot overwrite the live connection status.
    status("Waiting for live Arcane Vault snapshot · open Native if data is missing", false);
    provider.watchPath(SNAPSHOT_PATH);
  }

  window.icueEvents = { onICUEInitialized: watcherReady };
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
  $("settingsToggle").addEventListener("click", () => {
    const panel = $("settingsPanel");
    panel.hidden = !panel.hidden;
    $("settingsToggle").setAttribute("aria-expanded", String(!panel.hidden));
  });
  applyLayout();
  if (typeof pluginFilesystemprovider_initialized !== "undefined" && pluginFilesystemprovider_initialized)
    watcherReady();
  else if (typeof iCUE_initialized !== "undefined" && iCUE_initialized)
    watcherReady();

  // Never render synthetic deck or collection values in the released widget.
  // A missing iCUE provider remains a clearly labeled unavailable state.
}());
