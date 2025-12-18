<%@ Page Language="C#" AutoEventWireup="true" CodeFile="ForgotPassword.aspx.cs" Inherits="EventManagementWeb.Account.ForgotPassword" %>

<!DOCTYPE html>
<html lang="vi">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Quên mật khẩu</title>
    <link href="../Assets/css/base/reset.css" rel="stylesheet" />
    <link href="../Assets/css/style.css" rel="stylesheet" />
    <link href="../Assets/css/pages/forgot_pass.css" rel="stylesheet" />

</head>
<body>
    <form id="form1" runat="server">
        <div class="forgot_password-box">
            <h2 class="title">Quên mật khẩu?</h2>
            <p class="desc">Nhập địa chỉ email được liên kết với tài khoản của bạn và chúng tôi sẽ gửi cho bạn liên kết để đặt lại mật khẩu.</p>

            <div class="form-group">
                <label for="email">Email</label>
                <input type="email" id="email" name="email"
                    class="form-control" placeholder="Nhập email của bạn"
                    value="<%= Request.Form["email"] %>" required />
            </div>

            <% if (!string.IsNullOrEmpty(Message))
                { %>
            <div class="mgs <%= MessageClass %>">
                <%= Message %>
            </div>
            <% } %>

            <button type="submit" name="btnAction" value="send_link" class="btn-send">
                Gửi liên kết đặt lại
           
            </button>

            <div class="cta">
                <a href="Login.aspx" class='cta-link'>Quay lại Đăng nhập
                </a>
            </div>

        </div>
    </form>
</body>
</html>
