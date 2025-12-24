using MySql.Data.MySqlClient;
using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Web.UI;

namespace EventManagementWeb.Admin
{
    public partial class CreateNewUser : AdminBasePage
    {
        public string FullName = "";
        public string Email = "";
        public string Phone = "";
        public string Role = "Employee";
        public bool SendEmail = false;

        public string Message = "";
        public string MessageClass = "";

        public bool IsViewMode = false; // Chế độ xem chi tiết
        public string PageTitle = "Tạo nhân viên mới";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["UserId"] == null || Session["Role"]?.ToString() != "Admin")
            {
                Response.Redirect("~/Account/Login.aspx");
                return;
            }

            // Kiểm tra có id không → chế độ View
            if (!string.IsNullOrEmpty(Request.QueryString["id"]))
            {
                int userId;
                if (int.TryParse(Request.QueryString["id"], out userId))
                {
                    IsViewMode = true;
                    PageTitle = "Chi tiết nhân viên";

                    if (!IsPostBack)
                    {
                        LoadUserDetail(userId);
                    }
                }
            }

            if (Request.HttpMethod == "POST" && Request.Form["btnAction"] == "create" && !IsViewMode)
            {
                HandleCreateUser();
                return;
            }

            // Giữ giá trị form khi có lỗi (chỉ ở chế độ tạo mới)
            if (IsPostBack && !IsViewMode)
            {
                FullName = Request.Form["txtFullName"]?.Trim() ?? "";
                Email = Request.Form["txtEmail"]?.Trim().ToLower() ?? "";
                Phone = Request.Form["txtPhone"]?.Trim() ?? "";
                Role = Request.Form["ddlRole"] ?? "Employee";
                SendEmail = Request.Form["chkSendEmail"] != null;
            }
        }

        private void LoadUserDetail(int userId)
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;

            string sql = "SELECT FullName, Email, Phone, Role FROM Users WHERE Id = @id AND IsDeleted = 0";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    conn.Open();
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            FullName = reader["FullName"].ToString();
                            Email = reader["Email"].ToString();
                            Phone = reader["Phone"]?.ToString() ?? "";
                            Role = reader["Role"].ToString();
                        }
                        else
                        {
                            SetMessage("Không tìm thấy nhân viên.", false);
                        }
                    }
                }
            }
        }

        private void HandleCreateUser()
        {
            FullName = Request.Form["txtFullName"]?.Trim() ?? "";
            Email = Request.Form["txtEmail"]?.Trim().ToLower() ?? "";
            Phone = Request.Form["txtPhone"]?.Trim() ?? "";
            Role = Request.Form["ddlRole"] ?? "Employee";
            SendEmail = Request.Form["chkSendEmail"] != null;

            // Validate
            if (string.IsNullOrEmpty(FullName) || string.IsNullOrEmpty(Email))
            {
                SetMessage("Vui lòng nhập đầy đủ Họ và tên và Email.", false);
                return;
            }

            if (!Email.Contains("@") || !Email.Contains("."))
            {
                SetMessage("Email không hợp lệ.", false);
                return;
            }

            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                using (MySqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        // Kiểm tra email trùng
                        string checkSql = "SELECT COUNT(*) FROM Users WHERE Email = @email AND IsDeleted = 0";
                        using (MySqlCommand cmd = new MySqlCommand(checkSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@email", Email);
                            if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                            {
                                // Không rollback transaction vì chưa insert gì
                                SetMessage("Email này đã được sử dụng.", false);
                                return; // Giữ lại trang + hiển thị lỗi
                            }
                        }

                        // Tạo mật khẩu tạm thời
                        string tempPassword = GenerateRandomPassword(10);

                        // Hash bằng BCrypt (đồng bộ với login)
                        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(tempPassword);

                        // Insert user
                        string insertSql = @"
                            INSERT INTO Users (FullName, Email, Phone, Password, Role, IsActive, CreatedAt)
                            VALUES (@fullName, @email, @phone, @password, @role, 1, NOW())";

                        using (MySqlCommand cmd = new MySqlCommand(insertSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@fullName", FullName);
                            cmd.Parameters.AddWithValue("@email", Email);
                            cmd.Parameters.AddWithValue("@phone", string.IsNullOrEmpty(Phone) ? (object)DBNull.Value : Phone);
                            cmd.Parameters.AddWithValue("@password", hashedPassword);
                            cmd.Parameters.AddWithValue("@role", Role);
                            cmd.ExecuteNonQuery();
                        }

                        trans.Commit();

                        // Gửi email (nếu chọn)
                        if (SendEmail)
                        {
                            string loginLink = Request.Url.GetLeftPart(UriPartial.Authority) + ResolveUrl("~/Account/Login.aspx");
                            string subject = "Tài khoản EventHub của bạn đã được tạo";
                            string body = GetWelcomeEmailTemplate(FullName, Email, tempPassword, loginLink);

                            try
                            {
                                SendEmailNotification(Email, subject, body);
                            }
                            catch
                            {
                                // Không ảnh hưởng đến việc tạo user
                            }
                        }

                        Response.Redirect("UserManagement.aspx?msg=create_success", false);
                        Context.ApplicationInstance.CompleteRequest();
                    }
                    catch (Exception ex)
                    {
                        trans.Rollback();
                        System.Diagnostics.Debug.WriteLine("CreateUser Error: " + ex.Message);
                        SetMessage("Lỗi hệ thống khi tạo nhân viên. Vui lòng thử lại.", false);
                    }
                }
            }
        }

        private string GenerateRandomPassword(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            var random = new Random();
            var result = new System.Text.StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                result.Append(chars[random.Next(chars.Length)]);
            }
            return result.ToString();
        }

        private string GetWelcomeEmailTemplate(string fullName, string email, string tempPassword, string loginLink)
        {
            return $@"<html>
                <head>
                    <style>
                        body {{ font-family: 'Inter', sans-serif; color: #333; background: #f8f9fa; }}
                        .container {{ max-width: 600px; margin: 30px auto; padding: 30px; background: white; border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,0.1); }}
                        h2 {{ color: #0d6efd; }}
                        .info {{ background: #e3f2fd; padding: 20px; border-radius: 8px; margin: 20px 0; }}
                        .button {{
                            display: inline-block;
                            padding: 14px 28px;
                            background-color: #0d6efd;
                            color: white !important;
                            text-decoration: none;
                            font-weight: bold;
                            border-radius: 8px;
                            margin: 20px 0;
                            font-size: 16px;
                        }}
                        .footer {{ margin-top: 40px; color: #666; font-size: 14px; text-align: center; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <h2>Xin chào <strong>{fullName}</strong>,</h2>
                        <p>Chúc mừng! Tài khoản của bạn trên hệ thống <strong>Quản lý sự kiện nội bộ</strong> đã được tạo thành công.</p>
                        
                        <div class='info'>
                            <p><strong>Thông tin đăng nhập:</strong></p>
                            <p>• Email: <strong>{email}</strong></p>
                            <p>• Mật khẩu tạm thời: <strong>{tempPassword}</strong></p>
                        </div>
                        
                        <p style='text-align: center;'>
                            <a href='{loginLink}' class='button'>Đăng nhập ngay</a>
                        </p>
                        
                        <p><strong>Lưu ý quan trọng:</strong> Vui lòng đổi mật khẩu ngay lần đăng nhập đầu tiên để bảo mật tài khoản.</p>
                        
                        <div class='footer'>
                            <p>Trân trọng,<br><strong>Ban quản trị hệ thống</strong></p>
                        </div>
                    </div>
                </body>
                </html>";
        }

        private void SendEmailNotification(string toEmail, string subject, string body)
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
        }

        private void SetMessage(string text, bool isSuccess)
        {
            Message = text;
            MessageClass = isSuccess ? "success-label" : "error-label";
        }
    }
}