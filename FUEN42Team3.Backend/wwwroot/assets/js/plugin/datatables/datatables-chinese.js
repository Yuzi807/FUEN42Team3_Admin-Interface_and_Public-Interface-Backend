/*!
 * DataTables 中文語言設定
 * 這個檔案用於設定 DataTables 的預設中文語言
 */

// 擴充 DataTables 的預設語言設定
if (typeof $.fn.dataTable !== 'undefined') {
    $.extend(true, $.fn.dataTable.defaults, {
        "language": {
            "sProcessing": "處理中...",
            "sLengthMenu": "顯示 _MENU_ 項結果",
            "sZeroRecords": "沒有匹配結果",
            "sInfo": "顯示第 _START_ 至 _END_ 項結果，共 _TOTAL_ 項",
            "sInfoEmpty": "顯示第 0 至 0 項結果，共 0 項",
            "sInfoFiltered": "(由 _MAX_ 項結果過濾)",
            "sInfoPostFix": "",
            "sSearch": "關鍵字搜尋:",
            "sUrl": "",
            "sEmptyTable": "表格中無可用數據",
            "sLoadingRecords": "載入中...",
            "sInfoThousands": ",",
            "oPaginate": {
                "sFirst": "首頁",
                "sPrevious": "上頁",
                "sNext": "下頁",
                "sLast": "末頁"
            },
            "oAria": {
                "sSortAscending": ": 以升冪排列此列",
                "sSortDescending": ": 以降冪排列此列"
            }
        }
    });
}
