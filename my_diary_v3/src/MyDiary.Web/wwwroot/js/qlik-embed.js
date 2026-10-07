// ============================================================================
// qlik-embed.js — Qlik Sense embedding via the qlik-embed WEB COMPONENTS.
// Renders a whole sheet (ui="analytics/sheet", Qlik's own toolbar + export) or
// individual charts (ui="analytics/chart") — no iframe, no Capability API.
//
// Flow (called from QlikDashboard.razor):
//   1. render(cfg): plant the QPS session cookie silently (cfg.redeemUrl, no
//      redirect), load the qlik-embed library configured for the host/auth,
//      then inject <qlik-embed> element(s).
//   2. exportObject(id, format): native export via the qlik-embed element.
//
// cfg = { mode, host, prefix, redeemUrl, appId, sheetId, objectIds[],
//         render, embedScriptUrl, authType, clientId, redirectUri }
// ============================================================================

let _dotnet = null;
const _els = {};          // objectId -> <qlik-embed> element (for export)

// ── session: redeem the QPS ticket without navigating away ──────────────────
function plantSession(redeemUrl) {
    return new Promise((resolve) => {
        if (!redeemUrl) { resolve(false); return; }
        fetch(redeemUrl, { credentials: "include", mode: "no-cors" })
            .then(() => resolve(true))
            .catch(() => resolve(false));
        setTimeout(() => resolve(true), 2500);   // never hang the render
    });
}

// ── load + configure the qlik-embed web-components library (once) ───────────
function ensureEmbedLibrary(cfg) {
    if (window.customElements && window.customElements.get("qlik-embed")) return Promise.resolve();
    if (document.getElementById("qlik-embed-lib")) {
        // Already injected, wait for the custom element to register.
        return window.customElements.whenDefined("qlik-embed");
    }
    return new Promise((resolve, reject) => {
        const s = document.createElement("script");
        s.id = "qlik-embed-lib";
        s.type = "application/javascript";
        s.crossOrigin = "anonymous";
        s.src = cfg.embedScriptUrl;
        // Global config consumed by qlik-embed elements on the page.
        const host = cfg.host.replace(/\/+$/, "") + (cfg.prefix ? "/" + cfg.prefix.replace(/^\/+|\/+$/g, "") : "");
        s.setAttribute("data-host", host);
        s.setAttribute("data-auth-type", cfg.authType || "qsefe");
        if ((cfg.authType || "").toLowerCase() === "oauth2") {
            if (cfg.clientId) s.setAttribute("data-client-id", cfg.clientId);
            if (cfg.redirectUri) s.setAttribute("data-redirect-uri", cfg.redirectUri);
            s.setAttribute("data-access-token-storage", "session");
        }
        s.onload = () => resolve();
        s.onerror = () => reject(new Error("qlik-embed library load failed: " + cfg.embedScriptUrl));
        document.head.appendChild(s);
    });
}

export async function render(cfg, dotnetRef) {
    _dotnet = dotnetRef;
    const grid = document.getElementById("qlik-object-grid");
    if (!grid) return { ok: false, error: "grid-missing" };
    grid.innerHTML = "";
    for (const k in _els) delete _els[k];

    // ── Dev mock preview (no live Qlik host) ────────────────────────────────
    if (cfg.mode && cfg.mode.toLowerCase() === "mock") {
        grid.style.display = "block";
        grid.style.gridTemplateColumns = "none";
        grid.style.height = "auto";
        grid.style.overflow = "visible";
        renderMockDashboard(grid);
        return { ok: true, mock: true };
    }

    // ── Live Qlik: ticket session → qlik-embed library → <qlik-embed> ───────
    try {
        if (!cfg.embedScriptUrl) throw new Error("Qlik:EmbedScriptUrl is not configured.");
        await plantSession(cfg.redeemUrl);
        await ensureEmbedLibrary(cfg);

        const ids = cfg.objectIds && cfg.objectIds.length ? cfg.objectIds : [];
        const renderObjects = (cfg.render || "sheet").toLowerCase() === "objects" && ids.length > 0;

        if (renderObjects) {
            // One <qlik-embed ui="analytics/chart"> per object, in the grid.
            grid.style.display = "grid";
            grid.style.gridTemplateColumns = "repeat(2, minmax(0,1fr))";
            for (const id of ids) {
                const cell = document.createElement("div");
                cell.style.cssText = "min-height:340px;background:#fff;border:1px solid #e5e5e5;border-radius:4px;overflow:hidden;";
                const el = document.createElement("qlik-embed");
                el.setAttribute("ui", "analytics/chart");
                el.setAttribute("app-id", cfg.appId);
                el.setAttribute("object-id", id);
                el.style.cssText = "width:100%;height:100%;min-height:340px;display:block;";
                el.addEventListener("rendered", () => _dotnet && _dotnet.invokeMethodAsync("OnObjectRendered", id));
                el.addEventListener("error", (e) => _dotnet && _dotnet.invokeMethodAsync("OnQlikError", "Object " + id + " failed"));
                _els[id] = el;
                cell.appendChild(el);
                grid.appendChild(cell);
            }
        } else {
            // Whole sheet as-is (Qlik toolbar + native per-object export).
            grid.style.display = "block";
            grid.style.gridTemplateColumns = "none";
            const el = document.createElement("qlik-embed");
            el.setAttribute("ui", "analytics/sheet");
            el.setAttribute("app-id", cfg.appId);
            if (cfg.sheetId) el.setAttribute("sheet-id", cfg.sheetId);
            el.style.cssText = "width:100%;height:100%;min-height:560px;display:block;";
            el.addEventListener("error", () => _dotnet && _dotnet.invokeMethodAsync("OnQlikError", "Sheet failed to load"));
            grid.appendChild(el);
        }
        return { ok: true };
    } catch (e) {
        console.error("[qlik-embed] render failed", e);
        if (_dotnet) _dotnet.invokeMethodAsync("OnQlikError", (e && e.message) || String(e));
        return { ok: false, error: (e && e.message) || String(e) };
    }
}

// ── native export for a rendered object (qlik-embed exposes exportData) ─────
export async function exportObject(objectId, format) {
    const el = _els[objectId];
    if (!el) { console.warn("[qlik-embed] no element for", objectId); return { ok: false }; }
    const fmt = (format || "OOXML").toUpperCase();   // OOXML = xlsx, CSV_T = csv
    try {
        // qlik-embed elements expose an imperative handle once rendered.
        const api = el.exportData ? el : (el.getApi ? await el.getApi() : null);
        if (api && api.exportData) {
            const url = await api.exportData({ format: fmt });
            if (url) { const a = document.createElement("a"); a.href = url; a.download = ""; document.body.appendChild(a); a.click(); a.remove(); }
            return { ok: true, url };
        }
        console.warn("[qlik-embed] exportData not available; use the chart's built-in export menu.");
        return { ok: false, error: "export-not-available" };
    } catch (e) {
        console.error("[qlik-embed] export failed", e);
        return { ok: false, error: (e && e.message) || String(e) };
    }
}

// ── print the rendered dashboard to PDF (browser "Save as PDF") ─────────────
export function printPdf() {
    try { window.focus(); window.print(); return { ok: true }; }
    catch (e) { console.error("[qlik-embed] print failed", e); return { ok: false }; }
}

export function dispose() {
    for (const k in _els) delete _els[k];
    const grid = document.getElementById("qlik-object-grid");
    if (grid) grid.innerHTML = "";
}

// ── dev mock dashboard, rendered natively (no iframe, no docker dependency) ──
function renderMockDashboard(grid) {
    const NS = "http://www.w3.org/2000/svg";
    const el = (t, a) => { const e = document.createElementNS(NS, t); for (const k in a) e.setAttribute(k, a[k]); return e; };

    const months = ["Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec","Jan","Feb","Mar"];
    const deposits = [7.1,7.3,7.6,7.5,7.9,8.2,8.4,8.7,9.0,9.3,9.6,10.1];
    const advances = [4.2,4.3,4.5,4.6,4.8,5.0,5.1,5.3,5.5,5.7,5.9,6.2];
    const mix = [{l:"Savings",v:38,c:"#54a020"},{l:"Current",v:17,c:"#3f8ecc"},{l:"Term",v:28,c:"#f6a623"},{l:"Advances",v:17,c:"#d0021b"}];
    const regions = [
        {n:"Mumbai",d:3.8,g:9.1,t:0.86},{n:"Delhi",d:3.1,g:7.4,t:0.79},{n:"Chennai",d:2.4,g:6.2,t:0.72},
        {n:"Kolkata",d:1.9,g:5.1,t:0.65},{n:"Lucknow",d:1.6,g:4.4,t:0.58}];

    grid.innerHTML = `
      <style>
        .qm-note{padding:6px 10px;background:#fff8e1;border:1px solid #ffe0a3;border-radius:4px;margin-bottom:10px;font-size:12px;color:#8a6d00;}
        .qm-cards{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;}
        .qm-card{background:#fff;border:1px solid #e5e5e5;border-radius:4px;padding:14px;box-shadow:0 1px 2px rgba(0,0,0,.04);}
        .qm-card h3{margin:0 0 10px;font-size:11px;font-weight:600;letter-spacing:.4px;color:#6a6a6a;text-transform:uppercase;}
        .qm-val{font-size:28px;font-weight:700;line-height:1;color:#404040;} .qm-sub{font-size:12px;color:#6a6a6a;margin-top:6px;}
        .qm-up{color:#54a020;} .qm-down{color:#d0021b;}
        .qm-span2{grid-column:span 2;}
        .qm-legend{display:flex;gap:14px;flex-wrap:wrap;margin-top:8px;font-size:12px;color:#6a6a6a;}
        .qm-legend .k{display:inline-flex;align-items:center;gap:6px;} .qm-sw{width:11px;height:11px;border-radius:2px;display:inline-block;}
        .qm-table{width:100%;border-collapse:collapse;font-size:13px;}
        .qm-table th,.qm-table td{text-align:left;padding:7px 8px;border-bottom:1px solid #e5e5e5;}
        .qm-table th{color:#6a6a6a;font-weight:600;font-size:11px;text-transform:uppercase;letter-spacing:.4px;}
        .qm-num{text-align:right;font-variant-numeric:tabular-nums;}
        .qm-bar{background:#eef6e7;border-radius:3px;height:8px;overflow:hidden;} .qm-bar>i{display:block;height:100%;background:#54a020;}
        .qm-svg{width:100%;height:220px;display:block;}
      </style>
      <div class="qm-note">DEV MOCK — qlik-embed needs a live Qlik host. Showing a native sample dashboard (no iframe).</div>
      <div class="qm-cards">
        <div class="qm-card"><h3>Total Business</h3><div class="qm-val">₹ 12.84 Cr</div><div class="qm-sub qm-up">▲ 8.2% vs LY</div></div>
        <div class="qm-card"><h3>CASA Ratio</h3><div class="qm-val">41.6%</div><div class="qm-sub qm-up">▲ 1.3 pts</div></div>
        <div class="qm-card"><h3>NPA %</h3><div class="qm-val">2.9%</div><div class="qm-sub qm-down">▲ 0.2 pts</div></div>
        <div class="qm-card"><h3>Open Requests</h3><div class="qm-val">1,742</div><div class="qm-sub">312 breaching SLA</div></div>
        <div class="qm-card qm-span2"><h3>Monthly Business Trend (₹ Cr)</h3><svg id="qm-line" class="qm-svg" viewBox="0 0 480 220" preserveAspectRatio="none"></svg>
          <div class="qm-legend"><span class="k"><span class="qm-sw" style="background:#54a020"></span>Deposits</span><span class="k"><span class="qm-sw" style="background:#3f8ecc"></span>Advances</span></div></div>
        <div class="qm-card qm-span2"><h3>Business Mix by Product</h3><svg id="qm-donut" class="qm-svg" viewBox="0 0 480 220"></svg>
          <div class="qm-legend">${mix.map(m=>`<span class="k"><span class="qm-sw" style="background:${m.c}"></span>${m.l}</span>`).join("")}</div></div>
        <div class="qm-card qm-span2"><h3>Region-wise Deposits (₹ Cr)</h3><svg id="qm-bar" class="qm-svg" viewBox="0 0 480 220"></svg></div>
        <div class="qm-card qm-span2"><h3>Top Regions — Performance</h3>
          <table class="qm-table"><thead><tr><th>Region</th><th>Deposits</th><th>Growth</th><th style="width:110px">Target</th></tr></thead>
          <tbody>${regions.map(r=>`<tr><td>${r.n}</td><td class="qm-num">₹ ${r.d.toFixed(1)} Cr</td><td class="qm-num" style="color:#54a020">▲ ${r.g}%</td><td><div class="qm-bar"><i style="width:${Math.round(r.t*100)}%"></i></div></td></tr>`).join("")}</tbody></table></div>
      </div>`;

    (function(){ const svg=grid.querySelector("#qm-line"),W=480,H=220,pad=28,max=Math.max(...deposits,...advances)*1.1;
        const x=i=>pad+i*(W-pad*2)/(months.length-1),y=v=>H-pad-(v/max)*(H-pad*2);
        for(let g=0;g<=4;g++){const yy=pad+g*(H-pad*2)/4;svg.appendChild(el("line",{x1:pad,y1:yy,x2:W-pad,y2:yy,stroke:"#eee"}));}
        const draw=(arr,c)=>{let d="";arr.forEach((v,i)=>d+=(i?"L":"M")+x(i)+" "+y(v)+" ");svg.appendChild(el("path",{d,fill:"none",stroke:c,"stroke-width":2.5}));arr.forEach((v,i)=>svg.appendChild(el("circle",{cx:x(i),cy:y(v),r:3,fill:c})));};
        draw(deposits,"#54a020");draw(advances,"#3f8ecc");
        months.forEach((m,i)=>{const t=el("text",{x:x(i),y:H-8,"font-size":9,fill:"#999","text-anchor":"middle"});t.textContent=m;svg.appendChild(t);});})();
    (function(){ const svg=grid.querySelector("#qm-donut"),cx=240,cy=110,r=78,ir=44,total=mix.reduce((s,m)=>s+m.v,0);let a=-Math.PI/2;
        mix.forEach(m=>{const a2=a+m.v/total*Math.PI*2,p=(ang,rad)=>[cx+Math.cos(ang)*rad,cy+Math.sin(ang)*rad];
        const[x1,y1]=p(a,r),[x2,y2]=p(a2,r),[x3,y3]=p(a2,ir),[x4,y4]=p(a,ir),big=(a2-a)>Math.PI?1:0;
        svg.appendChild(el("path",{d:`M${x1} ${y1} A${r} ${r} 0 ${big} 1 ${x2} ${y2} L${x3} ${y3} A${ir} ${ir} 0 ${big} 0 ${x4} ${y4} Z`,fill:m.c}));a=a2;});
        const t=el("text",{x:cx,y:cy+5,"font-size":18,"font-weight":700,fill:"#404040","text-anchor":"middle"});t.textContent="100%";svg.appendChild(t);})();
    (function(){ const svg=grid.querySelector("#qm-bar"),W=480,H=220,pad=30,max=Math.max(...regions.map(r=>r.d))*1.15,bw=(W-pad*2)/regions.length*0.55;
        regions.forEach((rg,i)=>{const cx=pad+i*(W-pad*2)/regions.length+(W-pad*2)/regions.length/2,h=(rg.d/max)*(H-pad*2),yy=H-pad-h;
        svg.appendChild(el("rect",{x:cx-bw/2,y:yy,width:bw,height:h,rx:2,fill:"#54a020"}));
        const v=el("text",{x:cx,y:yy-5,"font-size":10,fill:"#404040","text-anchor":"middle"});v.textContent=rg.d;svg.appendChild(v);
        const t=el("text",{x:cx,y:H-10,"font-size":9,fill:"#999","text-anchor":"middle"});t.textContent=rg.n;svg.appendChild(t);});})();
}

export default { render, exportObject, printPdf, dispose };
