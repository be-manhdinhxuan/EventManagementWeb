using System;
using System.Web.UI;
using MySql.Data.MySqlClient;
using System.Configuration;
using System.Net.Mail;
using System.Net;
using System.Web.Security;

namespace EventManagementWeb.Account
{
    public partial class ForgotPassword : Page
    {

        protected void Page_Load(object sender, EventArgs e)
        {

        }

        protected void btnSend_Click (object sender, EventArgs e)
        {
            string email = txtEmail.Text.Trim();
            if (string.IsNullOrEmpty(email))
            {
                ShowMessage("Vui lòng nhập email.", false);
                return;
            }

            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();

                // Kiểm tra xem email có tồn tại không
                string checkSql = "SELECT Id, FullName FROM Users WHERE Email = @Email AND IsActive = 1 AND IsDeleted = 0";
                int userId = 0;
                string fullName = "";

                using (MySqlCommand cmd = new MySqlCommand(checkSql, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    using (MySqlDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                        {
                            ShowMessage("Email không tồn tại hoặc tài khoản bị khóa.", false);
                            return;
                        }
                        userId = Convert.ToInt32(r["Id"]);
                        fullName = r["FullName"].ToString();
                    }
                }

                // Tạo token đặt lại mật khẩu
                string token = Guid.NewGuid().ToString("N") + DateTime.Now.Ticks.ToString();

                // Lưu token vào DB (hạn 15')
                string insertToken = @"INSERT INTO PasswordResetTokens (UserId, Token, ExpiresAt) 
                                   VALUES (@userId, @token, DATE_ADD(NOW(), INTERVAL 15 MINUTE))";
                using (MySqlCommand cmd = new MySqlCommand(insertToken, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@token", token);
                    cmd.ExecuteNonQuery();
                }

                // Gửi email đặt lại mật khẩu
                string resetLink = Request.Url.GetLeftPart(UriPartial.Authority) + 
                                   ResolveUrl("/Account/ResetPassword.aspx?token=" + token);

                string subject = "Đặt lại mật khẩu - Quản lý sự kiện nội bộ";
                string body = $@"<html>
                <head>
                    <style>
                        body {{ font-family: Segoe UI, Arial, sans-serif; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .button {{
                            display: inline-block;
                            padding: 12px 24px;
                            background-color: #0d6efd;
                            color: white !important;
                            text-decoration: none;
                            font-weight: bold;
                            border-radius: 6px;
                            margin: 15px 0;
                        }}
                        .footer {{ margin-top: 30px; font-size: 13px; color: #777; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <h2>Xin chào <strong>{fullName}</strong>,</h2>
                        <p>Bạn (hoặc ai đó) đã yêu cầu đặt lại mật khẩu cho tài khoản của bạn trong hệ thống Quản lý sự kiện nội bộ.</p>
                        <p>Vui lòng click vào nút bên dưới để đặt mật khẩu mới. Link này chỉ có hiệu lực trong <strong>15 phút</strong>.</p>
        
                        <p style='text-align: center;'>
                            <a href='{resetLink}' class='button'>Đặt lại mật khẩu</a>
                        </p>
        
                        <p>Nếu nút không hoạt động, bạn có thể copy và dán link sau vào trình duyệt:</p>
                        <p style='word-break: break-all; background:#f8f9fa; padding:10px; border-radius:4px;'>{resetLink}</p>
        
                        <p>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này. Tài khoản của bạn vẫn an toàn.</p>
        
                        <div class='footer'>
                            Trân trọng,<br>
                            <strong>Hệ thống Quản lý sự kiện nội bộ</strong>
                        </div>
                    </div>
                </body>
                </html>";

                bool sent = SendEmail(email, subject, body);

                if (sent)
                    ShowMessage("Đã gửi email đặt lại mật khẩu. Vui lòng kiểm tra hộp thư của bạn.", true);
                else
                    ShowMessage("Có lỗi xảy ra khi gửi email. Vui lòng thử lại sau.", false);

            }
        }

        private void ShowMessage(string text, bool isSuccess)
        {
            lblMessage.Text = text;
            lblMessage.CssClass = "msg " + (isSuccess ? "success-label" : "error-label");
            lblMessage.Visible = true;
        }

        private bool SendEmail(string toEmail, string subject, string body)
        {
            try
            {
                var fromAddress = new MailAddress("manhbeo.it8@gmail.com", "Quản lý sự kiện nội bộ");
                var toAddress = new MailAddress(toEmail);

                
                string fromPassword = ConfigurationManager.AppSettings["EmailPassword"];
                const string smtpHost = "smtp.gmail.com";
                const int smtpPort = 587;

                var smtp = new SmtpClient
                {
                    Host = smtpHost,
                    Port = smtpPort,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
                };

                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                })
                {
                    smtp.Send(message);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                return false;
            }
        }
    }
}