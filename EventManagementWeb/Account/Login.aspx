<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Login.aspx.cs" Inherits="EventManagementWeb.Account.Login" %>

<!DOCTYPE html>
<html lang="vi">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Đăng nhập</title>
    <link href="../Assets/css/base/reset.css" rel="stylesheet" />
    <link href="../Assets/css/style.css" rel="stylesheet" />
    <link href="../Assets/css/pages/login.css" rel="stylesheet" />
</head>
<body>
    <form id="form1" runat="server">
        <div id="toastContainer" class="toast-container">
        </div>
        <div class="login-box">
            <div class="logo-login">
                <img src="../Assets/images/logo.png" alt="Logo" />
            </div>

            <div class="form-group">
                <label for="email">Email</label>
                <input type="email" id="email" name="email"
                    class="form-control" placeholder="Nhập email của bạn"
                    value="<%= Request.Form["email"] %>" required />
            </div>

            <div class="form-group">
                <label for="password">Mật khẩu</label>
                <div class="input-password">
                    <input type="password" id="password" name="password"
                        class="form-control" placeholder="Nhập mật khẩu" required />
                    <img id="toggleIcon" src="../Assets/images/icon/eye-close.svg"
                        alt="Toggle" class="toggle-password" onclick="togglePasswordVisibility()" />
                </div>
            </div>

            <% if (!string.IsNullOrEmpty(ErrorMessage))
                { %>
            <div class="error-label"><%= ErrorMessage %></div>
            <% } %>

            <button type="submit" name="btnAction" value="login" class="btn-login">
                Đăng nhập
           
            </button>

            <div class="cta">
                <a href="ForgotPassword.aspx" class='cta-link'>Quên mật khẩu?
                </a>
            </div>
        </div>
    </form>
    <script type="text/javascript">
        function togglePasswordVisibility() {
            var passwordInput = document.getElementById('password');
            var toggleIcon = document.getElementById('toggleIcon');

            if (passwordInput.type === 'password') {
                passwordInput.type = 'text';
                toggleIcon.src = '../Assets/images/icon/eye-open.svg';
            } else {
                passwordInput.type = 'password';
                toggleIcon.src = '../Assets/images/icon/eye-close.svg';
            }
            passwordInput.focus();
        }

        function showToast(title, message, type) {
            const container = document.getElementById('toastContainer');
            if (!container) return;

            // Định nghĩa SVG cho các trạng thái
            let iconSvg = '';


            if (type === 'success') {
                iconSvg = `<svg width="27" height="27" viewBox="0 0 27 27" fill="none" xmlns="http://www.w3.org/2000/svg"> <path d="M13.3333 0C5.98533 0 0 5.98533 0 13.3333C0 20.6813 5.98533 26.6667 13.3333 26.6667C20.6813 26.6667 26.6667 20.6813 26.6667 13.3333C26.6667 5.98533 20.6813 0 13.3333 0ZM13.3333 2.66667C19.2402 2.66667 24 7.4265 24 13.3333C24 19.2402 19.2402 24 13.3333 24C7.4265 24 2.66667 19.2402 2.66667 13.3333C2.66667 7.4265 7.4265 2.66667 13.3333 2.66667ZM19.0573 8.39062L10.6667 16.7812L7.60937 13.724L5.72396 15.6094L10.6667 20.5521L20.9427 10.276L19.0573 8.39062Z" fill="currentColor"/></svg>`;
            } else if (type === 'error') {
                iconSvg = `<svg width="26" height="26" viewBox="0 0 26 26" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M11.7 16.9H14.3V19.5H11.7V16.9ZM11.7 6.5H14.3V14.3H11.7V6.5ZM12.987 0C5.811 0 0 5.824 0 13C0 20.176 5.811 26 12.987 26C20.176 26 26 20.176 26 13C26 5.824 20.176 0 12.987 0ZM13 23.4C7.254 23.4 2.6 18.746 2.6 13C2.6 7.254 7.254 2.6 13 2.6C18.746 2.6 23.4 7.254 23.4 13C23.4 18.746 18.746 23.4 13 23.4Z" fill="currentColor"/>
                            </svg>`;
            } else if (type === 'warning') {
                iconSvg = `<svg width="30" height="26" viewBox="0 0 30 26" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M0 26L15 0L30 26H0ZM13.6364 17.7895H16.3636V10.9474H13.6364V17.7895ZM15 21.8947C15.3864 21.8947 15.7105 21.7634 15.9723 21.5006C16.2332 21.2388 16.3636 20.914 16.3636 20.5263C16.3636 20.1386 16.2332 19.8138 15.9723 19.552C15.7105 19.2893 15.3864 19.1579 15 19.1579C14.6136 19.1579 14.29 19.2893 14.0291 19.552C13.7673 19.8138 13.6364 20.1386 13.6364 20.5263C13.6364 20.914 13.7673 21.2388 14.0291 21.5006C14.29 21.7634 14.6136 21.8947 15 21.8947ZM4.70455 23.2632H25.2955L15 5.47368L4.70455 23.2632Z" fill="currentColor"/>
                            </svg>`;
            }

            const html = `
            <div class="alert ${type} alert-toast">
                ${iconSvg} 
                <div class="alert-body">
                    <h3 class="alert-title">${title}</h3>
                    <p class="alert-desc">${message}</p>
                </div>
            </div>
        `;

            const tempDiv = document.createElement('div');
            tempDiv.innerHTML = html;
            const alertElement = tempDiv.firstChild;
            container.appendChild(alertElement);

            // Tự động xóa sau 5 giây
            setTimeout(() => {
                alertElement.style.opacity = '0';
                alertElement.style.transition = 'opacity 0.5s';
                setTimeout(() => alertElement.remove(), 500);
            }, 5000);
        }
    </script>
</body>
</html>
