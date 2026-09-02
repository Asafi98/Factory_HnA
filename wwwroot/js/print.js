/**
 * printReport(elementId)
 * Opens a clean, professionally formatted print window for any report section.
 * Reads the live DOM from the Blazor component and rebuilds it in a
 * white-background, print-optimised page.
 */
window.printReport = function (elementId) {
    var el = document.getElementById(elementId);
    if (!el) { alert('Report area not found.'); return; }

    /* ── Collect meta strings rendered as hidden data-attributes ── */
    var titleEl    = el.querySelector('[data-report-title]');
    var periodEl   = el.querySelector('[data-report-period]');
    var reportTitle  = titleEl  ? titleEl.dataset.reportTitle  : 'Husna Aijaz — Report';
    var reportPeriod = periodEl ? periodEl.dataset.reportPeriod : new Date().toLocaleDateString();

    var w = window.open('', '_blank', 'width=1000,height=750');
    w.document.write(`<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<title>${reportTitle}</title>
<style>
/* ── Reset & base ── */
*,*::before,*::after{box-sizing:border-box;margin:0;padding:0}
body{font-family:'Segoe UI',Arial,sans-serif;font-size:12px;color:#111;background:#fff;padding:0}

/* ── Print page setup ── */
@page{size:A4 portrait;margin:18mm 14mm 14mm 14mm}

/* ── Letterhead ── */
.rpt-letterhead{
    display:grid;grid-template-columns:1fr auto;align-items:center;
    padding-bottom:14px;border-bottom:3px solid #1a1a1a;margin-bottom:18px
}
.rpt-letterhead-left h1{font-size:22px;font-weight:800;letter-spacing:.5px;color:#1a1a1a}
.rpt-letterhead-left p{font-size:11px;color:#555;margin-top:2px}
.rpt-letterhead-right{text-align:right;font-size:11px;color:#555;line-height:1.6}
.rpt-letterhead-right strong{display:block;font-size:13px;color:#1a1a1a;margin-bottom:2px}

/* ── Summary KPI strip ── */
.rpt-kpi-strip{
    display:grid;grid-template-columns:repeat(4,1fr);gap:0;
    border:1.5px solid #ccc;border-radius:4px;overflow:hidden;margin-bottom:20px
}
.rpt-kpi{padding:10px 14px;border-right:1px solid #ddd}
.rpt-kpi:last-child{border-right:none}
.rpt-kpi-label{font-size:9px;font-weight:700;text-transform:uppercase;letter-spacing:.7px;color:#777;margin-bottom:3px}
.rpt-kpi-value{font-size:17px;font-weight:800;color:#1a1a1a}
.rpt-kpi-sub{font-size:9px;color:#999;margin-top:1px}
.rpt-kpi.green .rpt-kpi-value{color:#1a7a3a}
.rpt-kpi.red   .rpt-kpi-value{color:#c0392b}
.rpt-kpi.blue  .rpt-kpi-value{color:#1565c0}

/* ── Section headings ── */
.rpt-section-heading{
    font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.8px;
    color:#333;border-bottom:2px solid #333;padding-bottom:5px;margin:22px 0 10px
}

/* ── Tables ── */
table{width:100%;border-collapse:collapse;margin-bottom:4px;page-break-inside:auto}
thead tr{background:#1a1a1a;color:#fff}
thead th{
    padding:7px 10px;font-size:10px;font-weight:700;
    text-transform:uppercase;letter-spacing:.5px;text-align:left;
    border:1px solid #1a1a1a;white-space:nowrap
}
thead th.num{text-align:right}
tbody tr{page-break-inside:avoid}
tbody tr:nth-child(even){background:#f7f7f7}
tbody td{
    padding:7px 10px;font-size:11px;border:1px solid #ddd;
    vertical-align:top;color:#222
}
tbody td.num{text-align:right;font-variant-numeric:tabular-nums}
tbody td.muted{color:#666;font-size:10px}
tfoot tr{background:#f0f0f0}
tfoot td{
    padding:7px 10px;font-size:11px;font-weight:700;
    border:1px solid #ccc;color:#111
}
tfoot td.num{text-align:right;font-variant-numeric:tabular-nums}

/* ── Item sub-list inside invoice row ── */
.item-list{margin:0;padding:0 0 0 14px;list-style:disc}
.item-list li{font-size:10px;color:#333;line-height:1.5}
.item-list li .note{color:#888;font-style:italic;font-size:9px}

/* ── Badge ── */
.badge{
    display:inline-block;padding:2px 7px;border-radius:10px;
    font-size:10px;font-weight:700;border:1px solid #aaa;white-space:nowrap
}

/* ── Expense category summary row ── */
.rpt-exp-grid{
    display:grid;grid-template-columns:repeat(auto-fill,minmax(140px,1fr));
    gap:8px;margin-top:10px
}
.rpt-exp-cat{
    border:1px solid #ddd;border-radius:4px;padding:8px 10px;background:#fafafa
}
.rpt-exp-cat-name{font-size:9px;color:#777;text-transform:uppercase;letter-spacing:.4px}
.rpt-exp-cat-val{font-size:13px;font-weight:700;color:#c0392b;margin-top:2px}

/* ── Report footer ── */
.rpt-footer{
    margin-top:28px;padding-top:10px;
    border-top:1px solid #ccc;
    display:grid;grid-template-columns:1fr 1fr 1fr;
    font-size:9px;color:#888
}
.rpt-footer span:nth-child(2){text-align:center}
.rpt-footer span:nth-child(3){text-align:right}

/* ── Utility ── */
.text-right{text-align:right}
.fw-bold{font-weight:700}
.color-pos{color:#1a7a3a}
.color-neg{color:#c0392b}

/* ── Ensure buttons etc are hidden ── */
button,.btn,.btn-row,.tab-bar,.no-print,.alert,input,select,
.sidebar,.modal-overlay,.page-header,.autocomplete-list{display:none!important}
.section-divider{display:none}
</style>
</head>
<body>
<div class="rpt-letterhead">
    <div class="rpt-letterhead-left">
        <h1>🏪 Husna Aijaz</h1>
        <p>Point of Sale System</p>
    </div>
    <div class="rpt-letterhead-right">
        <strong>${reportTitle}</strong>
        <span>${reportPeriod}</span><br>
        <span>Generated: ${new Date().toLocaleString('en-PK',{day:'2-digit',month:'short',year:'numeric',hour:'2-digit',minute:'2-digit'})}</span>
    </div>
</div>
${buildReportBody(el)}
<div class="rpt-footer">
    <span>Husna Aijaz POS System</span>
    <span>${reportPeriod}</span>
    <span>Printed: ${new Date().toLocaleDateString('en-PK',{day:'2-digit',month:'short',year:'numeric'})}</span>
</div>
</body></html>`);
    w.document.close();
    w.focus();
    setTimeout(function(){ w.print(); }, 700);
};

/* ── buildReportBody ──────────────────────────────────────────────────
   Reads the Blazor-rendered DOM and converts it into clean print HTML.
   Avoids copying dark-mode inline styles; rebuilds each section from
   semantic structure.
──────────────────────────────────────────────────────────────────────── */
function buildReportBody(el) {
    var html = '';

    /* ── KPI strip ── */
    var statCards = el.querySelectorAll('.stat-card');
    if (statCards.length) {
        html += '<div class="rpt-kpi-strip">';
        statCards.forEach(function(card) {
            var cls = card.classList.contains('green') ? ' green'
                    : card.classList.contains('red')   ? ' red'
                    : card.classList.contains('blue')  ? ' blue' : '';
            var label = (card.querySelector('.stat-label') || {}).textContent || '';
            var value = (card.querySelector('.stat-value') || {}).textContent || '';
            var sub   = (card.querySelector('.stat-sub')   || {}).textContent || '';
            html += '<div class="rpt-kpi' + cls + '">'
                  + '<div class="rpt-kpi-label">' + esc(label) + '</div>'
                  + '<div class="rpt-kpi-value">' + esc(value) + '</div>'
                  + '<div class="rpt-kpi-sub">'   + esc(sub)   + '</div>'
                  + '</div>';
        });
        html += '</div>';
    }

    /* ── Tables – iterate over every <table> in the report area ── */
    var tables = el.querySelectorAll('table');
    tables.forEach(function(tbl) {
        /* Section heading: look for the .section-divider just before the table's ancestor */
        var wrap = tbl.closest('div');
        var divider = wrap ? wrap.querySelector('.section-divider') : null;
        if (divider) {
            html += '<div class="rpt-section-heading">' + esc(divider.textContent) + '</div>';
        }

        html += '<table>';

        /* thead */
        var thead = tbl.querySelector('thead');
        if (thead) {
            html += '<thead><tr>';
            thead.querySelectorAll('th').forEach(function(th) {
                var align = (th.style.textAlign === 'right' || th.classList.contains('num')) ? ' class="num"' : '';
                html += '<th' + align + '>' + esc(th.textContent) + '</th>';
            });
            html += '</tr></thead>';
        }

        /* tbody */
        var tbody = tbl.querySelector('tbody');
        if (tbody) {
            html += '<tbody>';
            tbody.querySelectorAll('tr').forEach(function(tr) {
                html += '<tr>';
                tr.querySelectorAll('td').forEach(function(td) {
                    var isNum  = td.style.textAlign === 'right';
                    var isMuted= parseFloat(td.style.opacity||'1') < 1 || td.style.color === 'rgb(136, 136, 136)';
                    var cls = isNum ? ' class="num"' : (isMuted ? ' class="muted"' : '');

                    /* Special: item list inside invoice row */
                    var ul = td.querySelector('ul');
                    if (ul) {
                        html += '<td' + cls + '><ul class="item-list">';
                        ul.querySelectorAll('li').forEach(function(li) {
                            html += '<li>' + sanitizeLi(li) + '</li>';
                        });
                        html += '</ul></td>';
                        return;
                    }

                    /* Badge */
                    var badge = td.querySelector('.badge');
                    if (badge) {
                        html += '<td' + cls + '><span class="badge">' + esc(badge.textContent) + '</span></td>';
                        return;
                    }

                    /* Amount cells: preserve colour hint */
                    var colStyle = td.style.color || '';
                    var extraCls = '';
                    if (colStyle.includes('25c850') || colStyle.includes('1a7a')) extraCls = ' color-pos';
                    else if (colStyle.includes('ff6060') || colStyle.includes('c039')) extraCls = ' color-neg';
                    if (extraCls) cls = ' class="num' + extraCls + '"';

                    html += '<td' + cls + '>' + esc(td.textContent.trim()) + '</td>';
                });
                html += '</tr>';
            });
            html += '</tbody>';
        }

        /* tfoot */
        var tfoot = tbl.querySelector('tfoot');
        if (tfoot) {
            html += '<tfoot>';
            tfoot.querySelectorAll('tr').forEach(function(tr) {
                html += '<tr>';
                tr.querySelectorAll('td').forEach(function(td) {
                    var isNum = td.style.textAlign === 'right';
                    var cls = isNum ? ' class="num"' : '';
                    html += '<td' + cls + '>' + esc(td.textContent.trim()) + '</td>';
                });
                html += '</tr>';
            });
            html += '</tfoot>';
        }

        html += '</table>';
    });

    return html;
}

function sanitizeLi(li) {
    var text = li.textContent || '';
    var noteSpan = li.querySelector('.note');
    if (noteSpan) {
        var noteText = noteSpan.textContent || '';
        text = text.replace(noteText, '').trim();
        text += '<span class="note"> (' + esc(noteText) + ')</span>';
    }
    return text;
}

/* ── esc(str) ──
   HTML escape helper — prevents code injection via innerHTML
── */
function esc(str) {
    if (!str) return '';
    var d = document.createElement('div');
    d.textContent = String(str);
    return d.innerHTML;
}

/* ═════════════════════════════════════════════════════════════════════════════════════
   printInvoice(data)
   Prints a formatted invoice with all customer, item, and payment details.
   Called from Invoices.razor → PrintInvoice()
   data = {
     invoiceId, customerName, outlet, invoiceDate, paymentType,
     trialDate, createdBy, isDoorstep, invoiceType, subtotal, discountPct,
     discountAmount, totalAmount, amountPayed, balance, invoiceNotes, isVoid,
     items: JSON string → [{productName, unitPrice, quantity, lineTotal, notes}]
   }
═════════════════════════════════════════════════════════════════════════════════════ */
window.printInvoice = function (data) {
    var items = [];
    try { items = JSON.parse(data.items || '[]'); } catch(e) {}

    var w = window.open('', '_blank', 'width=900,height=750');

    /* ── Build items HTML ── */
    var itemsHtml = '<table class="inv-table"><thead><tr><th>Product</th><th style="text-align:right;width:80px">Unit Price</th><th style="text-align:center;width:60px">Qty</th><th style="text-align:right;width:90px">Total</th><th style="text-align:left;width:120px">Notes</th></tr></thead><tbody>';
    if (items.length) {
        items.forEach(function(it) {
            itemsHtml += '<tr>'
                      + '<td style="font-weight:500">' + esc(it.productName) + '</td>'
                      + '<td style="text-align:right">Rs. ' + parseFloat(it.unitPrice||0).toFixed(2) + '</td>'
                      + '<td style="text-align:center">' + esc(String(it.quantity)) + '</td>'
                      + '<td style="text-align:right;font-weight:600">Rs. ' + parseFloat(it.lineTotal||0).toFixed(2) + '</td>'
                      + '<td style="color:#666;font-size:10px">' + esc(it.notes || '') + '</td>'
                      + '</tr>';
        });
    } else {
        itemsHtml += '<tr><td colspan="5" style="text-align:center;color:#555;padding:20px">No items in this invoice</td></tr>';
    }
    itemsHtml += '</tbody></table>';

    var printed = new Date().toLocaleString('en-PK', {day:'2-digit',month:'short',year:'numeric',hour:'2-digit',minute:'2-digit'});
    var voidBadge = data.isVoid ? '<span style="color:#ff3333;font-weight:700;font-size:18px"> [VOID]</span>' : '';

    w.document.write(`<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<title>Invoice #${esc(String(data.invoiceId))}</title>
<style>
*,*::before,*::after{box-sizing:border-box;margin:0;padding:0}
body{font-family:'Segoe UI',Arial,sans-serif;font-size:11px;color:#111;background:#fff;padding:0}
@page{size:A4 portrait;margin:16mm 14mm 14mm 14mm}

/* Letterhead */
.inv-header{display:grid;grid-template-columns:1fr auto;align-items:center;padding-bottom:12px;border-bottom:3px solid #111;margin-bottom:16px}
.inv-header-left h1{font-size:24px;font-weight:800;color:#111;letter-spacing:-0.5px}
.inv-header-left p{font-size:10px;color:#666;margin-top:3px}
.inv-header-right{text-align:right;font-size:11px;color:#555;line-height:1.7}
.inv-header-right .inv-num{font-size:20px;font-weight:800;color:#111}

/* Invoice meta strip */
.inv-meta{display:grid;grid-template-columns:repeat(2,1fr);gap:0;border:1.5px solid #ccc;border-radius:4px;overflow:hidden;margin-bottom:18px}
.inv-meta-cell{padding:10px 14px;border-right:1px solid #ddd;border-bottom:1px solid #ddd}
.inv-meta-cell:nth-child(even){border-right:none}
.inv-meta-cell:nth-child(n+3){border-bottom:none}
.inv-meta-label{font-size:8px;font-weight:700;text-transform:uppercase;letter-spacing:.6px;color:#888;margin-bottom:3px}
.inv-meta-value{font-size:12px;font-weight:600;color:#111}

/* Items table */
.inv-table{width:100%;border-collapse:collapse;margin:18px 0}
.inv-table thead tr{background:#1a1a1a;color:#fff}
.inv-table thead th{padding:7px 10px;font-size:9px;font-weight:700;text-transform:uppercase;letter-spacing:.4px;text-align:left;border:1px solid #1a1a1a}
.inv-table tbody tr:nth-child(even){background:#f9f9f9}
.inv-table tbody td{padding:8px 10px;font-size:10px;border:1px solid #ddd;color:#222}

/* Totals section */
.inv-totals{margin-top:20px;border-top:2px solid #333;padding-top:12px}
.inv-totals-row{display:grid;grid-template-columns:1fr 140px;gap:20px;padding:6px 0;align-items:center}
.inv-totals-row.grand{border-top:1px solid #ddd;padding-top:8px;font-weight:700;font-size:13px;color:#1a1a1a}
.inv-totals-label{text-align:left;font-weight:500;color:#333}
.inv-totals-value{text-align:right;font-weight:600;font-variant-numeric:tabular-nums}
.inv-totals-value.neg{color:#c0392b}
.inv-totals-value.pos{color:#1a7a3a}

/* Info boxes */
.inv-info{margin-top:16px;padding:10px 14px;background:#f5f5f5;border:1px solid #ddd;border-radius:4px;font-size:10px;line-height:1.6;color:#444}
.inv-info-title{font-weight:700;color:#111;margin-bottom:4px;text-transform:uppercase;letter-spacing:.3px}

/* Footer */
.inv-footer{margin-top:28px;padding-top:10px;border-top:1px solid #ccc;display:grid;grid-template-columns:1fr 1fr 1fr;font-size:8px;color:#888;text-align:center}
.inv-footer span:first-child{text-align:left}
.inv-footer span:last-child{text-align:right}

/* Signature area */
.sig-row{display:grid;grid-template-columns:1fr 1fr;gap:40px;margin-top:28px}
.sig-box{border-top:1.5px solid #555;padding-top:6px;font-size:9px;color:#777;text-align:center}

.badge-void{display:inline-block;padding:2px 6px;background:#ff3333;color:#fff;border-radius:3px;font-size:9px;font-weight:700}
</style>
</head>
<body>

<div class="inv-header">
  <div class="inv-header-left">
    <h1>🏪 HUSNA AIJAZ</h1>
    <p>Point of Sale System</p>
  </div>
  <div class="inv-header-right">
    <span class="inv-num">INVOICE #${esc(String(data.invoiceId))}</span><br>
    <span>${printed}</span>
  </div>
</div>

<!-- Meta info grid -->
<div class="inv-meta">
  <div class="inv-meta-cell">
    <div class="inv-meta-label">Customer Name</div>
    <div class="inv-meta-value">${esc(data.customerName)}</div>
  </div>
  <div class="inv-meta-cell">
    <div class="inv-meta-label">Invoice Date</div>
    <div class="inv-meta-value">${esc(data.invoiceDate)}</div>
  </div>
  <div class="inv-meta-cell">
    <div class="inv-meta-label">Outlet</div>
    <div class="inv-meta-value">${esc(data.outlet || '—')}</div>
  </div>
  <div class="inv-meta-cell">
    <div class="inv-meta-label">Payment Type</div>
    <div class="inv-meta-value">${esc(data.paymentType || '—')}</div>
  </div>
  <div class="inv-meta-cell">
    <div class="inv-meta-label">Invoice Type</div>
    <div class="inv-meta-value">${esc(data.invoiceType || '—')}</div>
  </div>
  <div class="inv-meta-cell">
    <div class="inv-meta-label">Created By</div>
    <div class="inv-meta-value">${esc(data.createdBy || '—')}</div>
  </div>
  ${ data.trialDate ? '<div class="inv-meta-cell"><div class="inv-meta-label">Trial Date</div><div class="inv-meta-value">' + esc(data.trialDate) + '</div></div>' : '' }
  ${ data.isDoorstep ? '<div class="inv-meta-cell"><div class="inv-meta-label">Type</div><div class="inv-meta-value">🏠 Door Step</div></div>' : '' }
</div>

<!-- Items -->
${itemsHtml}

<!-- Totals -->
<div class="inv-totals">
  <div class="inv-totals-row">
    <div class="inv-totals-label">Subtotal</div>
    <div class="inv-totals-value">Rs. ${parseFloat(data.subtotal||0).toFixed(2)}</div>
  </div>
  ${ data.discountPct > 0 ? '<div class="inv-totals-row"><div class="inv-totals-label">Discount (' + data.discountPct + '%)</div><div class="inv-totals-value">- Rs. ' + parseFloat(data.discountAmount||0).toFixed(2) + '</div></div>' : '' }
  <div class="inv-totals-row grand">
    <div class="inv-totals-label">TOTAL AMOUNT</div>
    <div class="inv-totals-value">Rs. ${parseFloat(data.totalAmount||0).toFixed(2)}</div>
  </div>
  <div class="inv-totals-row">
    <div class="inv-totals-label">Amount Paid</div>
    <div class="inv-totals-value pos">Rs. ${parseFloat(data.amountPayed||0).toFixed(2)}</div>
  </div>
  <div class="inv-totals-row">
    <div class="inv-totals-label">Balance Due</div>
    <div class="inv-totals-value ${ data.balance <= 0 ? 'pos' : 'neg' }">Rs. ${parseFloat(Math.abs(data.balance||0)).toFixed(2)}</div>
  </div>
</div>

<!-- Notes -->
${ data.invoiceNotes ? '<div class="inv-info"><div class="inv-info-title">Invoice Notes</div>' + esc(data.invoiceNotes) + '</div>' : '' }

<!-- Signature area -->
<div class="sig-row">
  <div class="sig-box">Authorized By</div>
  <div class="sig-box">Received By Customer</div>
</div>

<div class="inv-footer">
  <span>Husna Aijaz POS</span>
  <span>Invoice #${esc(String(data.invoiceId))}</span>
  <span>Printed: ${printed}</span>
</div>

${data.isVoid ? '<div style="margin-top:20px;text-align:center"><span class="badge-void">VOID INVOICE</span></div>' : ''}

</body></html>`);
    w.document.close();
    w.focus();
    setTimeout(function(){ w.print(); }, 600);
};

/* ══════════════════════════════════════════════════════════════════
   printOrderSlip(data)
   Prints a formatted order slip with selected customer measurements.
   Called from Orders.razor → PrintOrderSlip()
   data = {
     orderId, invoiceId, customerName, outlet, status,
     trialDate, deliveryDate, notes,
     items: JSON string  → [{productName, quantity, notes}]
     measurements: JSON string → [{title, fields:[{k,v}]}]
══════════════════════════════════════════════════════════════════ */
window.printOrderSlip = function (data) {
    var items        = [];
    var measurements = [];
    try { items        = JSON.parse(data.items        || '[]'); } catch(e){}
    try { measurements = JSON.parse(data.measurements || '[]'); } catch(e){}

    var w = window.open('', '_blank', 'width=900,height=750');

    /* ── Build items HTML ── */
    var itemsHtml = '';
    if (items.length) {
        itemsHtml = '<table class="slip-table"><thead><tr><th>Item</th><th style="width:60px;text-align:center">Qty</th><th>Notes</th></tr></thead><tbody>';
        items.forEach(function(it) {
            itemsHtml += '<tr><td>' + esc(it.productName) + '</td>'
                       + '<td style="text-align:center;font-weight:700">' + esc(String(it.quantity)) + '</td>'
                       + '<td class="muted">' + esc(it.notes || '') + '</td></tr>';
        });
        itemsHtml += '</tbody></table>';
    } else {
        itemsHtml = '<p class="empty">No items recorded.</p>';
    }

    /* ── Build measurements HTML ── */
    var measHtml = '';
    if (measurements.length) {
        measurements.forEach(function(block) {
            var hasFields = block.fields && block.fields.length;
            var hasScan   = !!block.scanDataUri;
            if (!hasFields && !hasScan) return;

            measHtml += '<div class="meas-block">'
                      + '<div class="meas-title">' + esc(block.title) + '</div>';

            /* Scanned size form — shown instead of typing when present */
            if (hasScan) {
                if (block.scanIsPdf) {
                    measHtml += '<embed src="' + block.scanDataUri + '" type="application/pdf" class="meas-scan-pdf" />';
                } else {
                    measHtml += '<img src="' + block.scanDataUri + '" class="meas-scan-img" alt="Scanned ' + esc(block.title) + ' size form" />';
                }
            }

            if (hasFields) {
                measHtml += '<table class="meas-table"><tbody>';
                // Two fields per row
                for (var i = 0; i < block.fields.length; i += 2) {
                    measHtml += '<tr>';
                    measHtml += '<td class="meas-key">'   + esc(block.fields[i].k)   + '</td>'
                              + '<td class="meas-val">'   + esc(block.fields[i].v)   + '</td>';
                    if (block.fields[i+1]) {
                        measHtml += '<td class="meas-key">' + esc(block.fields[i+1].k) + '</td>'
                                  + '<td class="meas-val">' + esc(block.fields[i+1].v) + '</td>';
                    } else {
                        measHtml += '<td colspan="2"></td>';
                    }
                    measHtml += '</tr>';
                }
                measHtml += '</tbody></table>';
            }

            measHtml += '</div>';
        });
    }

    var invoiceRef = data.invoiceId ? ' &nbsp;|&nbsp; Invoice #' + esc(String(data.invoiceId)) : '';
    var printed = new Date().toLocaleString('en-PK', {day:'2-digit',month:'short',year:'numeric',hour:'2-digit',minute:'2-digit'});

    w.document.write(`<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<title>Order #${esc(String(data.orderId))} — ${esc(data.customerName)}</title>
<style>
*,*::before,*::after{box-sizing:border-box;margin:0;padding:0}
body{font-family:'Segoe UI',Arial,sans-serif;font-size:12px;color:#111;background:#fff;padding:0}
@page{size:A4 portrait;margin:16mm 14mm 14mm 14mm}

/* Letterhead */
.slip-header{display:grid;grid-template-columns:1fr auto;align-items:center;padding-bottom:12px;border-bottom:3px solid #111;margin-bottom:16px}
.slip-header-left h1{font-size:22px;font-weight:800;color:#111}
.slip-header-left p{font-size:11px;color:#555;margin-top:2px}
.slip-header-right{text-align:right;font-size:11px;color:#555;line-height:1.7}
.slip-header-right .order-num{font-size:18px;font-weight:800;color:#111}

/* Order meta strip */
.slip-meta{display:grid;grid-template-columns:repeat(3,1fr);gap:0;border:1.5px solid #ccc;border-radius:4px;overflow:hidden;margin-bottom:18px}
.slip-meta-cell{padding:9px 14px;border-right:1px solid #ddd}
.slip-meta-cell:last-child{border-right:none}
.slip-meta-label{font-size:9px;font-weight:700;text-transform:uppercase;letter-spacing:.6px;color:#888;margin-bottom:3px}
.slip-meta-value{font-size:13px;font-weight:700;color:#111}
.slip-meta-value.status-pending    {color:#b45309}
.slip-meta-value.status-progress   {color:#1565c0}
.slip-meta-value.status-trial      {color:#6b21a8}
.slip-meta-value.status-delivered  {color:#1a7a3a}

/* Section heading */
.slip-section{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.8px;color:#333;border-bottom:2px solid #333;padding-bottom:4px;margin:18px 0 10px}

/* Items table */
.slip-table{width:100%;border-collapse:collapse;margin-bottom:4px}
.slip-table thead tr{background:#1a1a1a;color:#fff}
.slip-table thead th{padding:6px 10px;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.4px;text-align:left;border:1px solid #1a1a1a}
.slip-table tbody tr:nth-child(even){background:#f7f7f7}
.slip-table tbody td{padding:7px 10px;font-size:11px;border:1px solid #ddd;color:#222}
.slip-table tbody td.muted{color:#777;font-size:10px}

/* Measurements */
.meas-block{margin-bottom:16px;page-break-inside:avoid}
.meas-title{font-size:11px;font-weight:800;text-transform:uppercase;letter-spacing:.6px;background:#1a1a1a;color:#fff;padding:5px 10px;border-radius:3px 3px 0 0}
.meas-table{width:100%;border-collapse:collapse}
.meas-table tr:nth-child(even){background:#f7f7f7}
.meas-table td{padding:5px 10px;font-size:11px;border:1px solid #ddd;width:25%}
.meas-key{color:#666;font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:.3px;width:18%}
.meas-val{font-weight:700;color:#111;width:7%}
.meas-scan-img{display:block;max-width:100%;max-height:320px;margin:0 auto 8px;border:1px solid #ccc;border-radius:0 0 3px 3px}
.meas-scan-pdf{display:block;width:100%;height:400px;border:1px solid #ccc;margin-bottom:8px}

/* Notes box */
.notes-box{border:1px solid #ddd;border-radius:4px;padding:10px 14px;font-size:11px;color:#333;background:#fafafa;margin-bottom:4px}

/* Footer */
.slip-footer{margin-top:28px;padding-top:10px;border-top:1px solid #ccc;display:grid;grid-template-columns:1fr 1fr 1fr;font-size:9px;color:#888}
.slip-footer span:nth-child(2){text-align:center}
.slip-footer span:nth-child(3){text-align:right}

/* Signature area */
.sig-row{display:grid;grid-template-columns:1fr 1fr 1fr;gap:24px;margin-top:28px}
.sig-box{border-top:1.5px solid #555;padding-top:6px;font-size:9px;color:#777;text-align:center}

.empty{color:#888;font-style:italic;font-size:11px;padding:8px 0}
</style>
</head>
<body>

<div class="slip-header">
  <div class="slip-header-left">
    <h1>🏪 Husna Aijaz</h1>
    <p>Point of Sale System</p>
  </div>
  <div class="slip-header-right">
    <span class="order-num">Order #${esc(String(data.orderId))}</span><br>
    <span>${esc(data.customerName)}${invoiceRef}</span><br>
    <span>Printed: ${printed}</span>
  </div>
</div>

<!-- Meta strip -->
<div class="slip-meta">
  <div class="slip-meta-cell">
    <div class="slip-meta-label">Customer</div>
    <div class="slip-meta-value">${esc(data.customerName)}</div>
  </div>
  <div class="slip-meta-cell">
    <div class="slip-meta-label">Outlet</div>
    <div class="slip-meta-value">${esc(data.outlet || '—')}</div>
  </div>
  <div class="slip-meta-cell">
    <div class="slip-meta-label">Status</div>
    <div class="slip-meta-value">${esc(data.status)}</div>
  </div>
  <div class="slip-meta-cell">
    <div class="slip-meta-label">Trial Date</div>
    <div class="slip-meta-value">${esc(data.trialDate || '—')}</div>
  </div>
  <div class="slip-meta-cell">
    <div class="slip-meta-label">Delivery Date</div>
    <div class="slip-meta-value">${esc(data.deliveryDate || '—')}</div>
  </div>
  <div class="slip-meta-cell">
    <div class="slip-meta-label">Invoice Ref</div>
    <div class="slip-meta-value">${data.invoiceId ? '#' + esc(String(data.invoiceId)) : '—'}</div>
  </div>
</div>

<!-- Items -->
<div class="slip-section">Items Ordered</div>
${itemsHtml}

${ data.notes ? '<div class="slip-section">Order Notes</div><div class="notes-box">' + esc(data.notes) + '</div>' : '' }

${ measHtml ? '<div class="slip-section">Measurements</div>' + measHtml : '' }

<!-- Signature area -->
<div class="sig-row">
  <div class="sig-box">Tailor / Workshop</div>
  <div class="sig-box">Quality Check</div>
  <div class="sig-box">Received By Customer</div>
</div>

<div class="slip-footer">
  <span>Husna Aijaz POS System</span>
  <span>Order #${esc(String(data.orderId))}</span>
  <span>Printed: ${printed}</span>
</div>

</body></html>`);
    w.document.close();
    w.focus();
    setTimeout(function(){ w.print(); }, 600);
};

/* ══════════════════════════════════════════════════════════════════
   printElement(elementId, title)
   Generic print helper for simple record/list views that are not
   shaped like a full report (customer detail, supplier detail,
   product/stock/expense tables, barcode preview, measurement sheet…).
   Clones the element's live HTML into a clean, light print window.
   Called from Customers/Products/Stock/Expenses/Suppliers/
   SupplierPurchases/Refunds/Measurements .razor pages.
══════════════════════════════════════════════════════════════════ */
window.printElement = function (elementId, title) {
    var el = document.getElementById(elementId);
    if (!el) { alert('Print area not found.'); return; }

    var w = window.open('', '_blank', 'width=1000,height=750');
    var printed = new Date().toLocaleString('en-PK', {day:'2-digit',month:'short',year:'numeric',hour:'2-digit',minute:'2-digit'});
    var docTitle = title || 'Husna Aijaz — Print';

    w.document.write(`<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<title>${esc(docTitle)}</title>
<style>
*,*::before,*::after{box-sizing:border-box}
body{font-family:'Segoe UI',Arial,sans-serif;font-size:12px;color:#111;background:#fff;margin:0;padding:20px}
@page{size:A4 portrait;margin:16mm 14mm 14mm 14mm}

/* Letterhead */
.pe-header{display:flex;justify-content:space-between;align-items:center;border-bottom:3px solid #1a1a1a;
  padding-bottom:12px;margin-bottom:16px}
.pe-header h1{font-size:18px;font-weight:800;color:#1a1a1a}
.pe-header span{font-size:11px;color:#555}

/* Neutralise the app's dark theme for print */
* { color:#111 !important; background:transparent !important; border-color:#ccc !important; }
.card,.modal-box,.modal-body,.modal-header,.modal-footer,.stat-card{background:#fff !important;border:none !important;padding:0 !important}
.card-title{color:#555 !important}

/* Tables */
table{width:100%;border-collapse:collapse;margin-bottom:10px}
thead th{background:#f0f0f0 !important;color:#111 !important;padding:7px 10px;font-size:10px;
  font-weight:700;text-transform:uppercase;text-align:left;border:1px solid #ccc}
tbody td{padding:7px 10px;font-size:11px;border:1px solid #ddd}
tfoot td{padding:7px 10px;font-size:11px;font-weight:700;border:1px solid #ccc}

/* Key/value rows and stat cards */
.info-row{display:flex;justify-content:space-between;padding:5px 0;border-bottom:1px solid #eee;font-size:12px}
.stat-card{border:1px solid #ddd !important;padding:10px !important;margin-bottom:8px}
.badge{border:1px solid #999 !important;padding:2px 7px;border-radius:10px;font-size:10px;font-weight:700}

/* Hide anything interactive/non-print */
button,.btn,.btn-row,.btn-print,input,select,textarea,.autocomplete-list,
.no-print,.alert,.modal-overlay{display:none !important}
</style>
</head>
<body>
<div class="pe-header">
  <h1>🏪 Husna Aijaz — Point of Sale System</h1>
  <span>${esc(docTitle)}${docTitle ? ' &nbsp;|&nbsp; ' : ''}Printed: ${printed}</span>
</div>
${el.innerHTML}
</body></html>`);
    w.document.close();
    w.focus();
    setTimeout(function(){ w.print(); }, 700);
};
