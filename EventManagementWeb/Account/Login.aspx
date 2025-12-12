<%@ Page Language="C#" AutoEventWireup="true" CodeFile="Login.aspx.cs" Inherits="EventManagementWeb.Account.Login" %>

<!DOCTYPE html>
<html lang="vi">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Đăng nhập - Quản lý sự kiện nội bộ</title>
    <link href="../Assets/css/reset.css" rel="stylesheet" />
    <link href="../Assets/css/style.css" rel="stylesheet" />
    <link href="../Assets/css/responsive.css" rel="stylesheet" />
    <style>
        /* CSS đẹp như hình bạn gửi */
        body { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); min-height: 100vh; display: flex; align-items: center; justify-content: center; font-family:'Segoe UI',sans-serif; }
        .login-box { background:#fff; padding:40px 50px; border-radius:12px; box-shadow:0 15px 35px rgba(0,0,0,0.15); width:100%; max-width:420px; }
        .login-box h2 { text-align:center; margin-bottom:30px; color:#333; }
        .form-group { margin-bottom:20px; }
        .form-group label { display:block; margin-bottom:8px; font-weight:600; color:#555; }
        input[type="email"], input[type="password"] { width:100%; padding:14px; border:1px solid #ddd; border-radius:8px; font-size:16px; }
        .btn-login { width:100%; padding:14px; background:#0d6efd; color:white; border:none; border-radius:8px; font-size:17px; cursor:pointer; margin-top:10px; }
        .btn-login:hover { background:#0b5ed7; }
        .error { color:#dc3545; text-align:center; margin-top:15px; font-weight:500; }
        .logo-login { text-align:center; margin-bottom:25px; }
        .logo-login img { height:60px; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <div class="login-box">
            <div class="logo-login">
                <img src="../Assets/images/logo.png" alt="Logo" />
            </div>
            <h2>Đăng nhập hệ thống</h2>

            <div class="form-group">
                <label>Email</label>
                <asp:TextBox ID="txtEmail" runat="server" placeholder="nhập email" TextMode="Email" CssClass="form-control"></asp:TextBox>
            </div>

            <div class="form-group">
                <label>Mật khẩu</label>
                <asp:TextBox ID="txtPassword" runat="server" placeholder="nhập mật khẩu" TextMode="Password" CssClass="form-control"></asp:TextBox>
            </div>

            <asp:Button ID="btnLogin" runat="server" Text="Đăng nhập" CssClass="btn-login" OnClick="btnLogin_Click" />

            <asp:Label ID="lblError" runat="server" CssClass="error" Text=""></asp:Label>
        </div>
    </form>
</body>
</html>