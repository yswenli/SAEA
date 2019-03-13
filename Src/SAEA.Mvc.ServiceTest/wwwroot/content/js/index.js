$(function () {

    //初始化

    $("#dataTxt").hide();

    $("#methodSel").change(function () {
        if ($("#methodSel").val() === "POST") {
            $("#dataTxt").show();
        }
        else {
            $("#dataTxt").hide();
        }
    });

    $.get("/home/getlist", "", function (data) {
        $("#accordion").html(data);
    });

    //go按钮
    $("#runBtn").click(function () {
        var begin = (new Date()).getTime();

        $('#loading').modal('show');

        $("#alert-words").html("");

        var method = $("#methodSel").val();
        var url = $("#urlTxt").val();
        var data = $("#dataTxt").val();

        var jsonData = { "method": method, "url": encodeURIComponent(url), "data": encodeURIComponent(data) };

        $.ajax({
            type: "POST",
            url: "/home/request",
            contentType: "application/json; charset=utf-8",
            data: JSON.stringify(jsonData),
            dataType: "json",
            success: function (message) {
                var now = (new Date()).getTime() - begin;
                $("#alert-words").html("本次请求用时：" + now / 1000 + "秒");

                $("#reTxt").val(JSON.stringify(message));
                $('#loading').modal('hide');
            },
            error: function (message) {
                var now = (new Date()).getTime() - begin;
                $("#alert-words").html("本次请求用时：" + now / 1000 + "秒");

                $("#reTxt").val(message.responseText);
                $('#loading').modal('hide');
            }
        });

    });

    //添加组
    $("#addGroupBtn").click(function () {

        var groupName = $("#addGroupNameTxt").val();

        if (groupName !== "") {

            $.post("/home/addgroup", "groupName=" + encodeURIComponent(groupName), function (data) {

                if (data === "1") {

                    $.get("/home/getlist", "", function (data) {

                        $("#accordion").html(data);
                    });

                    $("#addGroupModal").modal('hide');
                }
            });
        }
    });

    //编辑组
    $("#accordion").on("click", ".group-edit", function () {
        $(this).parent().next().css("visibility", "visible");
        $(this).parent().hide();
    });

    $("#accordion").on("click", ".edit-group-btn", function () {
        var groupID = $(this).attr("data-id");
        var groupName = $(this).parent().prev().val();

        $.post("/home/updategroup", "groupID=" + groupID + "&groupName=" + encodeURIComponent(groupName), function (data) {

            if (data === "1") {
                $.get("/home/getlist", "", function (data) {
                    $("#accordion").html(data);
                });
            }
        });
    });


    //保存按钮
    $("#saveBtn").click(function () {
        $.get("/home/getgroups", null, function (data) {
            $("#groupIDsel").html(data);
        });
    });

    $("#addItemBtn").click(function () {

        var groupID = $("#groupIDsel").val();
        var title = $("#addItemTxt").val();
        var method = $("#methodSel").val();
        var url = $("#urlTxt").val();
        var json = $("#dataTxt").val();

        if (groupID === "") {
            alert("请选择组！");
            return;
        }
        if (title === "") {
            alert("title不能为空！");
            return;
        }
        if (url === "") {
            alert("Url地址不能为空！");
            return;
        }

        $.post("/home/additem", `groupID=${groupID}&title=${encodeURIComponent(title)}&method=${method}&url=${encodeURIComponent(url)}&json=${encodeURIComponent(json)}`, function (data) {
            $.get("/home/getlist", "groupID=" + groupID, function (data) {
                $("#accordion").html(data);
                $("#addItemModal").modal('hide');
            });
        });
    });

    //删除组
    $("#accordion").on("click", ".group-delete", function () {
        $("#deletegroupidTxt").val($(this).attr("data-id"));
    });

    $("#deletegroupBtn").click(function () {

        var groupID = $("#deletegroupidTxt").val();

        if (groupID !== "") {

            $.post("/home/removegroup", `groupID=${groupID}`, function (data) {

                $.get("/home/getlist", "", function (data) {
                    $("#accordion").html(data);
                });

                $('#deleteModal').modal('hide');
            });
        }
    });

    //删除项
    $("#accordion").on("click", ".delteItem", function () {
        $("#deleteitemidTxt").val($(this).attr("data-id"));
        $("#deleteitemgroupidTxt").val($(this).attr("data-group"));
    });

    $("#deleteitemBtn").click(function () {

        var groupID = $("#deleteitemgroupidTxt").val();

        var itemID = $("#deleteitemidTxt").val();

        if (groupID !== "" && itemID !== "") {

            $.post("/home/removeitem", `groupID=${groupID}&itemID=${itemID}`, function (data) {

                $.get("/home/getlist", "", function (data) {
                    $("#accordion").html(data);
                });

                $('#deleteItemModal').modal('hide');
            });
        }
    });

    //列表点击
    $("#accordion").on("click", ".urlLink", function () {
        $('#loading').modal('show');
        var groupID = $(this).attr("data-group");

        var itemID = $(this).attr("data-id");

        if (groupID !== "" && itemID !== "") {

            $.get("/home/getitem", `groupID=${groupID}&itemID=${itemID}`, function (data) {

                setTimeout("$('#loading').modal('hide')", 500);

                if (data !== undefined || data !== "") {
                    $("#methodSel").val(data.Method);
                    $("#methodSel").change();                    
                    $("#dataTxt").val(data.RequestJson);
                    $("#urlTxt").addClass("flicker");
                    setTimeout(function () {
                        $("#urlTxt").removeClass("flicker");
                        $("#urlTxt").val(data.Url);
                    }, 1000);
                }
            });
        }
    });

    //搜索
    $("#searchBtn").click(function () {

        var keywords = decodeURIComponent($("#searchTxt").val());

        $.post("/home/search", `keywords=${keywords}`, function (data) {
            $(".search-list").html(data);
        });
    });

    //搜索项点击
    $(".search-list").on("click", "div", function () {
        $('#searchModel').modal('hide');
        $('#loading').modal('show');

        var groupID = $(this).attr("data-group");

        var itemID = $(this).attr("data-id");

        if (groupID !== "" && itemID !== "") {

            $.get("/home/getitem", `groupID=${groupID}&itemID=${itemID}`, function (data) {

                setTimeout("$('#loading').modal('hide')", 500);

                if (data !== undefined || data !== "") {
                    $("#methodSel").val(data.Method);
                    $("#methodSel").change();
                    $("#dataTxt").val(data.RequestJson);
                    $("#urlTxt").addClass("flicker");
                    setTimeout(function () {
                        $("#urlTxt").removeClass("flicker");
                        $("#urlTxt").val(data.Url);
                    }, 1000);
                }
                
            });
        }
    });


    //导入
    $("#importDataBtn").click(function () {

        var json = $("#importDataTxt").val();

        if (json !== "") {
            $.post("/home/import", "json=" + encodeURIComponent(json), function (data) {
                if (data === "1") {
                    $.get("/home/getlist", "", function (data) {
                        $("#accordion").html(data);
                    });
                }
                else {
                    alert("数据导入失败！");
                }

                $("#importModal").modal('hide');
            });
        }
        else {
            alert("数据导入失败！");
            $("#importModal").modal('hide');
        }
    });

    //导出
    $("#exportBtn").click(function () {
        $.get("/home/export", null, function (data) {
            $("#exportDiv").html(JSON.stringify(data));
        });
    });

    //快捷键
    $("#urlTxt").keyup(function (event) {
        if (event.keyCode === 13) {
            $("#runBtn").click();
        }
    });

    $("#searchTxt").keyup(function (event) {
        if (event.keyCode === 13) {
            $("#searchBtn").click();
        }
    });

    $(window).keydown(function (event) {
        if (!((event.ctrlKey && event.keyCode === 83) || (event.key === "F5"))) return true;

        event.preventDefault();

        if (event.key === "F5") {
            $("#runBtn").click();
        }
        else
            $("#saveBtn").click();

        return false;
    });
});
