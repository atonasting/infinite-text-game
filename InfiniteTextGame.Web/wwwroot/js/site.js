$(function () {
    //toastr提示默认配置
    if (toastr) {
        toastr.options = {
            "closeButton": false,
            "debug": false,
            "newestOnTop": false,
            "progressBar": false,
            "positionClass": "toast-bottom-right",
            "preventDuplicates": true,
            "onclick": null,
            "showDuration": "300",
            "hideDuration": "1000",
            "timeOut": "5000",
            "extendedTimeOut": "1000",
            "showEasing": "swing",
            "hideEasing": "linear",
            "showMethod": "fadeIn",
            "hideMethod": "fadeOut"
        }
    }
});

//禁用按钮并追加loading图标
function buttonAddLoading(button) {
    $(button).prop("disabled", true);
    $(button).prepend('<span class="spinner-border spinner-border-sm"></span> ');
}
//恢复按钮并移除loading图标
function buttonRemoveLoading(button) {
    $(button).find("span.spinner-border:first").remove();
    $(button).prop("disabled", false);
}

//弹出Confirm对话框并绑定确认回调函数
function showConfirmBox(content, okCallback, cancelCallback) {
    $("#confirm-box-content").text(content);
    $("#confirm-box-okbutton").one("click", function (event) {
        $("#confirm-box").modal("hide");
        if (okCallback) okCallback();
    });

    $("#confirm-box-cancelbutton").one("click", function (event) {
        $("#confirm-box").modal("hide");
        if (cancelCallback) cancelCallback();
    });

    $("#confirm-box").modal({
        backdrop: "static"
    });
}

//启用返回顶端按钮
function enableBackToTopButton(showButtonPosition) {
    let buttonHtml = '<button type="button" class="btn btn-primary btn-floating btn-lg rounded-circle btn-back-to-top"><i class="fas fa-arrow-up"></i></button>';
    var backToTopButton = $(buttonHtml).appendTo('body');
    showButtonPosition = showButtonPosition ?? 60;//默认滚动60高度后开始显示按钮

    $(window).on('scroll', function () {
        if ($(window).scrollTop() > showButtonPosition) {
            backToTopButton.show();
        } else {
            backToTopButton.hide();
        }
    });

    backToTopButton.on("click", function () {
        $('html, body').animate({ scrollTop: 0 }, 500);
    });
}