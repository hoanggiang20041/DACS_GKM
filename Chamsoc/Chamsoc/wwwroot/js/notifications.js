// Function to show error message
function showErrorMessage(message) {
    Swal.fire({
        title: 'Thông báo',
        text: message,
        icon: 'error',
        confirmButtonText: 'Đóng',
        confirmButtonColor: '#3085d6',
        customClass: {
            popup: 'animated fadeInDown'
        }
    });
}

// Function to show success message
function showSuccessMessage(message) {
    Swal.fire({
        title: 'Thành công',
        text: message,
        icon: 'success',
        confirmButtonText: 'Đóng',
        confirmButtonColor: '#3085d6',
        customClass: {
            popup: 'animated fadeInDown'
        }
    });
}

// Function to show warning message
function showWarningMessage(message) {
    Swal.fire({
        title: 'Cảnh báo',
        text: message,
        icon: 'warning',
        confirmButtonText: 'Đóng',
        confirmButtonColor: '#3085d6',
        customClass: {
            popup: 'animated fadeInDown'
        }
    });
}

// Function to show info message
function showInfoMessage(message) {
    Swal.fire({
        title: 'Thông tin',
        text: message,
        icon: 'info',
        confirmButtonText: 'Đóng',
        confirmButtonColor: '#3085d6',
        customClass: {
            popup: 'animated fadeInDown'
        }
    });
}

// Check for TempData messages on page load
document.addEventListener('DOMContentLoaded', function() {
    const errorMessage = document.getElementById('errorMessage')?.value;
    const successMessage = document.getElementById('successMessage')?.value;
    const warningMessage = document.getElementById('warningMessage')?.value;
    const infoMessage = document.getElementById('infoMessage')?.value;

    if (errorMessage) showErrorMessage(errorMessage);
    if (successMessage) showSuccessMessage(successMessage);
    if (warningMessage) showWarningMessage(warningMessage);
    if (infoMessage) showInfoMessage(infoMessage);
}); 