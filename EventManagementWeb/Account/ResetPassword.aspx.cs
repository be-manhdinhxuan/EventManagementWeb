using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace EventManagementWeb.Account
{
    public partial class ResetPassword : System.Web.UI.Page
    {
        public string Message = "";
        public string MessageClass = "";

        protected void Page_Load(object sender, EventArgs e)
        {
            // 1. Kiểm tra Token trên QueryString (Dữ liệu nhận từ URL)
            string token = Request.QueryString["token"];

            if (string.IsNullOrEmpty(token))
            {
                // Nếu không có token, chuyển hướng về trang Login
                Response.Redirect("Login.aspx");
                return;
            }
            // 2. Xử lý khi người dùng nhấn nút "Đặt lại mật khẩu" (Dữ liệu nhận từ POST Body)
            if (Request.HttpMethod == "POST" && Request.Form["btnAction"] == "reset")
            {
                HandleResetPassword(token);
            }
        }

        private void HandleResetPassword(string token)
        {
            // Lấy dữ liệu thuần từ Request.Form
            string pass1 = Request.Form["newPassword"];
            string pass2 = Request.Form["confirmPassword"];

            // Kiểm tra logic mật khẩu
            if (pass1 != pass2)
            {
                SetMessage("Mật khẩu xác nhận không khớp", false);
                return;
            }
            if (pass1.Length < 8)
            {
                SetMessage("Mật khẩu phải ít nhất 8 ký tự", false);
                return;
            }

            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                try
                {
                    conn.Open();

                    // BƯỚC 1: Kiểm tra tính hợp lệ của Token trong Database
                    string sqlToken = @"SELECT UserId FROM PasswordResetTokens 
                                       WHERE Token = @token 
                                         AND ExpiresAt > NOW() 
                                         AND IsUsed = 0 LIMIT 1";

                    int userId = 0;
                    using (MySqlCommand cmd = new MySqlCommand(sqlToken, conn))
                    {
                        cmd.Parameters.AddWithValue("@token", token);
                        object result = cmd.ExecuteScalar();
                        if (result == null)
                        {
                            SetMessage("Liên kết đã hết hạn hoặc không hợp lệ", false);
                            return;
                        }
                        userId = Convert.ToInt32(result);
                    }

                    // BƯỚC 2: Lấy thông tin User để chuẩn bị tạo Session đăng nhập sau khi đổi pass
                    string email = "";
                    string role = "";
                    string sqlUser = "SELECT Email, Role FROM Users WHERE Id = @id";
                    using (MySqlCommand cmd = new MySqlCommand(sqlUser, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", userId);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                email = reader["Email"].ToString();
                                role = reader["Role"].ToString();
                            }
                        }
                    }

                    // BƯỚC 3: Hash mật khẩu mới và Cập nhật vào bảng Users
                    string hashedPassword = BCrypt.Net.BCrypt.HashPassword(pass1);
                    string sqlUpdate = "UPDATE Users SET Password = @pass WHERE Id = @id";
                    using (MySqlCommand cmd = new MySqlCommand(sqlUpdate, conn))
                    {
                        cmd.Parameters.AddWithValue("@pass", hashedPassword);
                        cmd.Parameters.AddWithValue("@id", userId);
                        cmd.ExecuteNonQuery();
                    }

                    // BƯỚC 4: Vô hiệu hóa Token đã sử dụng
                    string sqlMarkUsed = "UPDATE PasswordResetTokens SET IsUsed = 1 WHERE Token = @token";
                    using (MySqlCommand cmd = new MySqlCommand(sqlMarkUsed, conn))
                    {
                        cmd.Parameters.AddWithValue("@token", token);
                        cmd.ExecuteNonQuery();
                    }

                    // BƯỚC 5: Thiết lập đăng nhập tự động sau khi đổi mật khẩu thành công
                    if (!string.IsNullOrEmpty(email))
                    {
                        Session["UserId"] = userId;
                        Session["Email"] = email;
                        Session["Role"] = role;

                        FormsAuthentication.SetAuthCookie(email, false);

                        // Điều hướng dựa trên quyền hạn
                        if (role == "Admin")
                            Response.Redirect("~/Admin/Dashboard.aspx");
                        else
                            Response.Redirect("~/User/Home.aspx");
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
            MessageClass = isSuccess ? "success-label" : "error-label";
        }
    }
}