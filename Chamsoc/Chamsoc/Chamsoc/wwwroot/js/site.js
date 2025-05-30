// Cấu hình toastr
toastr.options = {
    "closeButton": true,
    "debug": false,
    "newestOnTop": true,
    "progressBar": true,
    "positionClass": "toast-top-right",
    "preventDuplicates": false,
    "onclick": null,
    "showDuration": "300",
    "hideDuration": "1000",
    "timeOut": "5000",
    "extendedTimeOut": "1000",
    "showEasing": "swing",
    "hideEasing": "linear",
    "showMethod": "fadeIn",
    "hideMethod": "fadeOut"
};

// Hàm hiển thị thông báo thành công
function showSuccessMessage(message) {
    toastr.success(message);
}

// Hàm hiển thị thông báo lỗi
function showErrorMessage(message) {
    toastr.error(message);
}

// Hàm hiển thị thông báo thông tin
function showInfoMessage(message) {
    toastr.info(message);
}

// Hàm hiển thị thông báo cảnh báo
function showWarningMessage(message) {
    toastr.warning(message);
}

// Hiển thị thông báo từ TempData
$(document).ready(function() {
    if (tempData.successMessage) {
        showSuccessMessage(tempData.successMessage);
    }
    if (tempData.errorMessage) {
        showErrorMessage(tempData.errorMessage);
    }
    if (tempData.infoMessage) {
        showInfoMessage(tempData.infoMessage);
    }
    if (tempData.warningMessage) {
        showWarningMessage(tempData.warningMessage);
    }
}); 