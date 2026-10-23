/*
   Licensed to the Apache Software Foundation (ASF) under one or more
   contributor license agreements.  See the NOTICE file distributed with
   this work for additional information regarding copyright ownership.
   The ASF licenses this file to You under the Apache License, Version 2.0
   (the "License"); you may not use this file except in compliance with
   the License.  You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
var showControllersOnly = false;
var seriesFilter = "";
var filtersOnlySampleSeries = true;

/*
 * Add header in statistics table to group metrics by category
 * format
 *
 */
function summaryTableHeader(header) {
    var newRow = header.insertRow(-1);
    newRow.className = "tablesorter-no-sort";
    var cell = document.createElement('th');
    cell.setAttribute("data-sorter", false);
    cell.colSpan = 1;
    cell.innerHTML = "Requests";
    newRow.appendChild(cell);

    cell = document.createElement('th');
    cell.setAttribute("data-sorter", false);
    cell.colSpan = 3;
    cell.innerHTML = "Executions";
    newRow.appendChild(cell);

    cell = document.createElement('th');
    cell.setAttribute("data-sorter", false);
    cell.colSpan = 7;
    cell.innerHTML = "Response Times (ms)";
    newRow.appendChild(cell);

    cell = document.createElement('th');
    cell.setAttribute("data-sorter", false);
    cell.colSpan = 1;
    cell.innerHTML = "Throughput";
    newRow.appendChild(cell);

    cell = document.createElement('th');
    cell.setAttribute("data-sorter", false);
    cell.colSpan = 2;
    cell.innerHTML = "Network (KB/sec)";
    newRow.appendChild(cell);
}

/*
 * Populates the table identified by id parameter with the specified data and
 * format
 *
 */
function createTable(table, info, formatter, defaultSorts, seriesIndex, headerCreator) {
    var tableRef = table[0];

    // Create header and populate it with data.titles array
    var header = tableRef.createTHead();

    // Call callback is available
    if(headerCreator) {
        headerCreator(header);
    }

    var newRow = header.insertRow(-1);
    for (var index = 0; index < info.titles.length; index++) {
        var cell = document.createElement('th');
        cell.innerHTML = info.titles[index];
        newRow.appendChild(cell);
    }

    var tBody;

    // Create overall body if defined
    if(info.overall){
        tBody = document.createElement('tbody');
        tBody.className = "tablesorter-no-sort";
        tableRef.appendChild(tBody);
        var newRow = tBody.insertRow(-1);
        var data = info.overall.data;
        for(var index=0;index < data.length; index++){
            var cell = newRow.insertCell(-1);
            cell.innerHTML = formatter ? formatter(index, data[index]): data[index];
        }
    }

    // Create regular body
    tBody = document.createElement('tbody');
    tableRef.appendChild(tBody);

    var regexp;
    if(seriesFilter) {
        regexp = new RegExp(seriesFilter, 'i');
    }
    // Populate body with data.items array
    for(var index=0; index < info.items.length; index++){
        var item = info.items[index];
        if((!regexp || filtersOnlySampleSeries && !info.supportsControllersDiscrimination || regexp.test(item.data[seriesIndex]))
                &&
                (!showControllersOnly || !info.supportsControllersDiscrimination || item.isController)){
            if(item.data.length > 0) {
                var newRow = tBody.insertRow(-1);
                for(var col=0; col < item.data.length; col++){
                    var cell = newRow.insertCell(-1);
                    cell.innerHTML = formatter ? formatter(col, item.data[col]) : item.data[col];
                }
            }
        }
    }

    // Add support of columns sort
    table.tablesorter({sortList : defaultSorts});
}

$(document).ready(function() {

    // Customize table sorter default options
    $.extend( $.tablesorter.defaults, {
        theme: 'blue',
        cssInfoBlock: "tablesorter-no-sort",
        widthFixed: true,
        widgets: ['zebra']
    });

    var data = {"OkPercent": 81.25, "KoPercent": 18.75};
    var dataset = [
        {
            "label" : "FAIL",
            "data" : data.KoPercent,
            "color" : "#FF6347"
        },
        {
            "label" : "PASS",
            "data" : data.OkPercent,
            "color" : "#9ACD32"
        }];
    $.plot($("#flot-requests-summary"), dataset, {
        series : {
            pie : {
                show : true,
                radius : 1,
                label : {
                    show : true,
                    radius : 3 / 4,
                    formatter : function(label, series) {
                        return '<div style="font-size:8pt;text-align:center;padding:2px;color:white;">'
                            + label
                            + '<br/>'
                            + Math.round10(series.percent, -2)
                            + '%</div>';
                    },
                    background : {
                        opacity : 0.5,
                        color : '#000'
                    }
                }
            }
        },
        legend : {
            show : true
        }
    });

    // Creates APDEX table
    createTable($("#apdexTable"), {"supportsControllersDiscrimination": true, "overall": {"data": [0.8125, 500, 1500, "Total"], "isController": false}, "titles": ["Apdex", "T (Toleration threshold)", "F (Frustration threshold)", "Label"], "items": [{"data": [1.0, 500, 1500, "S3 Support tai chinh - danh sach vi"], "isController": false}, {"data": [1.0, 500, 1500, "S2 Support tai chinh - lich su payout"], "isController": false}, {"data": [0.0, 500, 1500, "S6 [BUG?] Support tai chinh - don cho quyet toan"], "isController": false}, {"data": [0.0, 500, 1500, "A1 [BUG?] Chua dang nhap - duyet tra hang"], "isController": false}, {"data": [1.0, 500, 1500, "S1 Support tai chinh - xem phi san"], "isController": false}, {"data": [1.0, 500, 1500, "M2 Monitor ghi - ban user"], "isController": false}, {"data": [1.0, 500, 1500, "Login monitor"], "isController": false}, {"data": [1.0, 500, 1500, "S5 Support admin roles - xem admin users"], "isController": false}, {"data": [1.0, 500, 1500, "P2 Doi chung - Support xem Users"], "isController": false}, {"data": [1.0, 500, 1500, "Login support"], "isController": false}, {"data": [1.0, 500, 1500, "M1 Monitor ghi - doi trang thai user"], "isController": false}, {"data": [1.0, 500, 1500, "P1 Doi chung - Monitor xem Dashboard"], "isController": false}, {"data": [1.0, 500, 1500, "S4 Support admin roles - xem roles"], "isController": false}, {"data": [1.0, 500, 1500, "M3 Monitor ghi - sua phi san"], "isController": false}, {"data": [0.0, 500, 1500, "M5 [BUG?] Monitor ghi - assign dispute"], "isController": false}, {"data": [1.0, 500, 1500, "M4 Monitor ghi - tao admin role"], "isController": false}]}, function(index, item){
        switch(index){
            case 0:
                item = item.toFixed(3);
                break;
            case 1:
            case 2:
                item = formatDuration(item);
                break;
        }
        return item;
    }, [[0, 0]], 3);

    // Create statistics table
    createTable($("#statisticsTable"), {"supportsControllersDiscrimination": true, "overall": {"data": ["Total", 16, 3, 18.75, 25.749999999999996, 2, 176, 3.0, 146.60000000000002, 176.0, 176.0, 15.32567049808429, 5.663875419061302, 10.76931423611111], "isController": false}, "titles": ["Label", "#Samples", "FAIL", "Error %", "Average", "Min", "Max", "Median", "90th pct", "95th pct", "99th pct", "Transactions/s", "Received", "Sent"], "items": [{"data": ["S3 Support tai chinh - danh sach vi", 1, 0, 0.0, 3.0, 3, 3, 3.0, 3.0, 3.0, 3.0, 333.3333333333333, 32.2265625, 266.2760416666667], "isController": false}, {"data": ["S2 Support tai chinh - lich su payout", 1, 0, 0.0, 3.0, 3, 3, 3.0, 3.0, 3.0, 3.0, 333.3333333333333, 32.2265625, 272.4609375], "isController": false}, {"data": ["S6 [BUG?] Support tai chinh - don cho quyet toan", 1, 1, 100.0, 10.0, 10, 10, 10.0, 10.0, 10.0, 10.0, 100.0, 15.625, 81.93359375], "isController": false}, {"data": ["A1 [BUG?] Chua dang nhap - duyet tra hang", 1, 1, 100.0, 2.0, 2, 2, 2.0, 2.0, 2.0, 2.0, 500.0, 49.31640625, 109.86328125], "isController": false}, {"data": ["S1 Support tai chinh - xem phi san", 1, 0, 0.0, 3.0, 3, 3, 3.0, 3.0, 3.0, 3.0, 333.3333333333333, 32.2265625, 268.8802083333333], "isController": false}, {"data": ["M2 Monitor ghi - ban user", 1, 0, 0.0, 3.0, 3, 3, 3.0, 3.0, 3.0, 3.0, 333.3333333333333, 32.2265625, 270.5078125], "isController": false}, {"data": ["Login monitor", 1, 0, 0.0, 176.0, 176, 176, 176.0, 176.0, 176.0, 176.0, 5.681818181818182, 5.509810014204546, 1.2872869318181819], "isController": false}, {"data": ["S5 Support admin roles - xem admin users", 1, 0, 0.0, 3.0, 3, 3, 3.0, 3.0, 3.0, 3.0, 333.3333333333333, 32.2265625, 269.2057291666667], "isController": false}, {"data": ["P2 Doi chung - Support xem Users", 1, 0, 0.0, 20.0, 20, 20, 20.0, 20.0, 20.0, 20.0, 50.0, 110.05859375, 41.9921875], "isController": false}, {"data": ["Login support", 1, 0, 0.0, 134.0, 134, 134, 134.0, 134.0, 134.0, 134.0, 7.462686567164179, 7.222189832089552, 1.6907649253731343], "isController": false}, {"data": ["M1 Monitor ghi - doi trang thai user", 1, 0, 0.0, 3.0, 3, 3, 3.0, 3.0, 3.0, 3.0, 333.3333333333333, 32.2265625, 271.1588541666667], "isController": false}, {"data": ["P1 Doi chung - Monitor xem Dashboard", 1, 0, 0.0, 31.0, 31, 31, 31.0, 31.0, 31.0, 31.0, 32.25806451612903, 17.83014112903226, 26.115171370967744], "isController": false}, {"data": ["S4 Support admin roles - xem roles", 1, 0, 0.0, 3.0, 3, 3, 3.0, 3.0, 3.0, 3.0, 333.3333333333333, 32.2265625, 269.2057291666667], "isController": false}, {"data": ["M3 Monitor ghi - sua phi san", 1, 0, 0.0, 4.0, 4, 4, 4.0, 4.0, 4.0, 4.0, 250.0, 24.169921875, 202.392578125], "isController": false}, {"data": ["M5 [BUG?] Monitor ghi - assign dispute", 1, 1, 100.0, 11.0, 11, 11, 11.0, 11.0, 11.0, 11.0, 90.9090909090909, 8.7890625, 74.3075284090909], "isController": false}, {"data": ["M4 Monitor ghi - tao admin role", 1, 0, 0.0, 3.0, 3, 3, 3.0, 3.0, 3.0, 3.0, 333.3333333333333, 32.2265625, 270.5078125], "isController": false}]}, function(index, item){
        switch(index){
            // Errors pct
            case 3:
                item = item.toFixed(2) + '%';
                break;
            // Mean
            case 4:
            // Mean
            case 7:
            // Median
            case 8:
            // Percentile 1
            case 9:
            // Percentile 2
            case 10:
            // Percentile 3
            case 11:
            // Throughput
            case 12:
            // Kbytes/s
            case 13:
            // Sent Kbytes/s
                item = item.toFixed(2);
                break;
        }
        return item;
    }, [[0, 0]], 0, summaryTableHeader);

    // Create error table
    createTable($("#errorsTable"), {"supportsControllersDiscrimination": false, "titles": ["Type of error", "Number of errors", "% in errors", "% in all samples"], "items": [{"data": ["400/Bad Request", 1, 33.333333333333336, 6.25], "isController": false}, {"data": ["404/Not Found", 1, 33.333333333333336, 6.25], "isController": false}, {"data": ["Test failed: code expected to equal /\\n\\n****** received  : [[[200]]]\\n\\n****** comparison: [[[403]]]\\n\\n/", 1, 33.333333333333336, 6.25], "isController": false}]}, function(index, item){
        switch(index){
            case 2:
            case 3:
                item = item.toFixed(2) + '%';
                break;
        }
        return item;
    }, [[1, 1]]);

        // Create top5 errors by sampler
    createTable($("#top5ErrorsBySamplerTable"), {"supportsControllersDiscrimination": false, "overall": {"data": ["Total", 16, 3, "400/Bad Request", 1, "404/Not Found", 1, "Test failed: code expected to equal /\\n\\n****** received  : [[[200]]]\\n\\n****** comparison: [[[403]]]\\n\\n/", 1, "", "", "", ""], "isController": false}, "titles": ["Sample", "#Samples", "#Errors", "Error", "#Errors", "Error", "#Errors", "Error", "#Errors", "Error", "#Errors", "Error", "#Errors"], "items": [{"data": [], "isController": false}, {"data": [], "isController": false}, {"data": ["S6 [BUG?] Support tai chinh - don cho quyet toan", 1, 1, "Test failed: code expected to equal /\\n\\n****** received  : [[[200]]]\\n\\n****** comparison: [[[403]]]\\n\\n/", 1, "", "", "", "", "", "", "", ""], "isController": false}, {"data": ["A1 [BUG?] Chua dang nhap - duyet tra hang", 1, 1, "400/Bad Request", 1, "", "", "", "", "", "", "", ""], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": [], "isController": false}, {"data": ["M5 [BUG?] Monitor ghi - assign dispute", 1, 1, "404/Not Found", 1, "", "", "", "", "", "", "", ""], "isController": false}, {"data": [], "isController": false}]}, function(index, item){
        return item;
    }, [[0, 0]], 0);

});
