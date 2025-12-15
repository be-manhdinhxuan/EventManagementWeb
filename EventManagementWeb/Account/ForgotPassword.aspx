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
                <label>Email</label>
                <asp:TextBox ID="txtEmail" runat="server" placeholder="Nhập email của bạn" TextMode="Email" CssClass="form-control" ClientIDMode="Static"></asp:TextBox>
            </div>
            <asp:Label ID="lblMessage" runat="server" CssClass="mgs" Visible="false"></asp:Label>

            <asp:Button ID="btnSend" runat="server" Text="Gửi liên kết đặt lại" CssClass="btn-send" OnClick="btnSend_Click" ClientIDMode="Static" />

            <div class="cta">
                <a href="Login.aspx" class='cta-link'>Quay lại Đăng nhập
                </a>
            </div>

        </div>
    </form>
</body>
</html>
