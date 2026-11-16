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

  try { selectedId = localStorage.getItem(selectionKey) || ""; } catch (_) {}

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

  function render(snapshot) {
    const decks = Array.isArray(snapshot?.decks) ? snapshot.decks.filter(d => d && d.id) : [];
    if (!decks.length) {
      $("deckName").textContent = "No saved decks";
      $("commander").textContent = "Add a deck in Arcane Vault Native";
      $("deckCount").textContent = "0 / 0";
      $("wins").textContent = "—"; $("losses").textContent = "—";
      $("rate").textContent = "—"; $("rateFill").style.width = "0%";
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
    const recent = snapshot.recentMatch;
    const recentIsSelected = recent && String(recent.deckName || "").trim().toLowerCase() ===
      String(deck.name || "").trim().toLowerCase();
    $("recent").textContent = recentIsSelected ? String(recent.result || "—").toUpperCase() : "—";
    $("recent").className = "feature-data latest" +
      (recentIsSelected && recent.result === "win" ? " win" :
       recentIsSelected && recent.result === "loss" ? " loss" : "");
    $("recentDescription").textContent = recentIsSelected ? "Latest confirmed result for selected deck" :
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
    provider.watchPath(SNAPSHOT_PATH);
    status("Watching Arcane Vault's local snapshot · open Native if data is missing", false);
  }

  window.icueEvents = { onICUEInitialized: watcherReady };
  window.pluginFilesystemproviderEvents = { onInitialized: watcherReady };
  $("prev").addEventListener("click", () => cycle(-1));
  $("next").addEventListener("click", () => cycle(1));
  if (typeof pluginFilesystemprovider_initialized !== "undefined" && pluginFilesystemprovider_initialized)
    watcherReady();
  else if (typeof iCUE_initialized !== "undefined" && iCUE_initialized)
    watcherReady();

  // Browser-only preview with unmistakable sample data. Never claim demo
  // numbers are read from a real user's saved collection.
  if (typeof window.iCUE === "undefined" && typeof window.plugins === "undefined") {
    $("previewLabel").textContent = "SAMPLE PREVIEW";
    readSnapshot({
      decks:[{id:"sample",name:"Demo Commander Deck",commander:"Sample commander",wins:12,losses:7,artUrl:""}],
      collection:{value:0,currency:"USD",asOf:"example"},
      recentMatch:{result:"win",deckName:"Demo Commander Deck"}
    } instanceof Object ? JSON.stringify({
      decks:[{id:"sample",name:"Demo Commander Deck",commander:"Sample commander",wins:12,losses:7,artUrl:""}],
      collection:{value:0,currency:"USD",asOf:"example"},
      recentMatch:{result:"win",deckName:"Demo Commander Deck"}
    }) : "");
    status("SAMPLE PREVIEW · iCUE not connected", true);
  }
}());
