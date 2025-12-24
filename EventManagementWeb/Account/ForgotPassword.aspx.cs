using System;
using System.Web.UI;
using MySql.Data.MySqlClient;
using System.Configuration;
using System.Net.Mail;
using System.Net;
using System.Collections.Generic;

namespace EventManagementWeb.Account
{
    public partial class ForgotPassword : Page
    {
        // Khai báo biến public để file .aspx có thể truy cập trực tiếp
        public string Message = "";
        public string MessageClass = "";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.HttpMethod == "POST" && Request.Form["btnAction"] == "send_link")
            {
                HandleForgotPassword();
            }
        }

        private void HandleForgotPassword()
        {
            string email = Request.Form["email"]?.Trim();

            if (string.IsNullOrEmpty(email))
            {
                SetMessage("Vui lòng nhập email.", false);
                return;
            }

            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                try
                {
                    conn.Open();

                    // Bước 1: Kiểm tra xem email có tồn tại và đang hoạt động không
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
                                SetMessage("Email không tồn tại hoặc tài khoản bị khóa.", false);
                                return;
                            }
                            userId = Convert.ToInt32(r["Id"]);
                            fullName = r["FullName"].ToString();
                        }
                    }

                    // Bước 2: Tạo token đặt lại mật khẩu (Dùng Guid để đảm bảo tính duy nhất)
                    string token = Guid.NewGuid().ToString("N") + DateTime.Now.Ticks.ToString();

                    // Bước 3: Lưu token vào DB (Hạn dùng 15 phút)
                    string insertToken = @"INSERT INTO PasswordResetTokens (UserId, Token, ExpiresAt) 
                                           VALUES (@userId, @token, DATE_ADD(NOW(), INTERVAL 15 MINUTE))";
                    using (MySqlCommand cmd = new MySqlCommand(insertToken, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@token", token);
                        cmd.ExecuteNonQuery();
                    }

                    // Bước 4: Xây dựng nội dung và gửi Email
                    string resetLink = Request.Url.GetLeftPart(UriPartial.Authority) +
                                       ResolveUrl("/Account/ResetPassword.aspx?token=" + token);

                    string subject = "Đặt lại mật khẩu - Quản lý sự kiện nội bộ";
                    string body = GetEmailTemplate(fullName, resetLink);

                    if (SendEmail(email, subject, body))
                    {
                        SetMessage("Đã gửi email đặt lại mật khẩu. Vui lòng kiểm tra hộp thư của bạn.", true);
                    }
                    else
                    {
                        SetMessage("Có lỗi xảy ra khi gửi email. Vui lòng thử lại sau.", false);
                    }
                }
                catch (Exception ex)
                {
                    SetMessage("Lỗi hệ thống: " + ex.Message, false);
                }
            }
        }

        private void SetMessage(string text, bool isSuccess)
        {
            Message = text;
            // Class này sẽ được in ra thẻ div ở file .aspx
            MessageClass = isSuccess ? "success-label" : "error-label";
        }

        private string GetEmailTemplate(string fullName, string resetLink)
        {
            return $@"<html>
                <head>
                    <style>
                        body {{ font-family: 'Segoe UI', Arial, sans-serif; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 8px; }}
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
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <h2>Xin chào <strong>{fullName}</strong>,</h2>
                        <p>Bạn đã yêu cầu đặt lại mật khẩu. Link này có hiệu lực trong 15 phút.</p>
                        <p style='text-align: center;'><a href='{resetLink}' class='button'>Đặt lại mật khẩu</a></p>
                        <p>Trân trọng,<br>Ban quản lý sự kiện</p>
                    </div>
                </body>
                </html>";
        }

        private bool SendEmail(string toEmail, string subject, string body)
        {
            try
            {
                string fromEmail = "manhbeo.it8@gmail.com";
                string fromPassword = ConfigurationManager.AppSettings["EmailPassword"];

                var fromAddress = new MailAddress(fromEmail, "Quản lý sự kiện nội bộ");
                var toAddress = new MailAddress(toEmail);

                var smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(fromEmail, fromPassword)
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
                System.Diagnostics.Debug.WriteLine("Email Error: " + ex.Message);
                return false;
            }
        }
    }
}