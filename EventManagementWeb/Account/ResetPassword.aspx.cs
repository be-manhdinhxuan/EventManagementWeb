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
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                string token = Request.QueryString["token"];
                if (string.IsNullOrEmpty(token))
                {
                    ShowMessage("Liên kết đặt lại mật khẩu không hợp lệ.", false);
                }
            }
        }

        protected void btnReset_Click (object sender, EventArgs e)
        {
            string token = Request.QueryString["token"];
            string pass1 = txtNewPass.Text;
            string pass2 = txtConfirm.Text;

            if (pass1 != pass2)
            {
                ShowMessage("Mật khẩu không khớp", false);
                return;
            }
            if (pass1.Length < 8)
            {
                ShowMessage("Mật khẩu phải ít nhất 8 ký tự", false);
                return;
            }

            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();

                string sql = @"SELECT prt.UserId 
                           FROM PasswordResetTokens prt
                           WHERE prt.Token = @token 
                             AND prt.ExpiresAt > NOW() 
                             AND prt.IsUsed = 0
                           LIMIT 1";

                int userId = 0;
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@token", token);
                    object result = cmd.ExecuteScalar();
                    if (result == null)
                    {
                        ShowMessage("Link đã hết hạn hoặc không hợp lệ", false);
                        return;
                    }
                    userId = Convert.ToInt32(result);
                }

                string email = string.Empty; ;
                string role = string.Empty;
                string sqlUserInfo = "SELECT Email, Role FROM Users WHERE Id = @id";
                using (MySqlCommand cmdUserInfo = new MySqlCommand(sqlUserInfo, conn))
                {
                    cmdUserInfo.Parameters.AddWithValue("@id", userId);
                    using (MySqlDataReader reader = cmdUserInfo.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            email = reader["Email"].ToString();
                            role = reader["Role"].ToString();
                        }
                    }
                }

                // Hash mật khẩu mới và cập nhật
                string hashed = BCrypt.Net.BCrypt.HashPassword(pass1);
                string updateUser = "UPDATE Users SET Password = @pass WHERE Id = @id";
                using (MySqlCommand cmd = new MySqlCommand(updateUser, conn))
                {
                    cmd.Parameters.AddWithValue("@pass", hashed);
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }

                // Đánh dấu token đã dùng
                string markUsed = "UPDATE PasswordResetTokens SET IsUsed = 1 WHERE Token = @token";
                using (MySqlCommand cmd = new MySqlCommand(markUsed, conn))
                {
                    cmd.Parameters.AddWithValue("@token", token);
                    cmd.ExecuteNonQuery();
                }

                ShowMessage("Đặt lại mật khẩu thành công!", true);

                
                // Tạo Session và FormsAuthentication Cookie
                if (!string.IsNullOrEmpty(email))
                {
                    Session["UserId"] = userId;
                    Session["Email"] = email;
                    Session["Role"] = role; // Lưu Role vào Session

                    FormsAuthentication.SetAuthCookie(email, false); // Đăng nhập tự động
                    
                    // Chuyển hướng dựa trên quyền
                    if (role == "Admin")
                    {
                        Response.Redirect("~/Admin/Dashboard.aspx");
                    }
                    else
                    {
                        Response.Redirect("~/User/Home.aspx");
                    }
                }
                else
                {
                    ShowMessage("Lỗi hệ thống: Không tìm thấy thông tin người dùng.", false);
                }
            }
        }

        private void ShowMessage(string text, bool isSuccess)
        {
            lblMessage.Text = text;
            lblMessage.CssClass = "msg " + (isSuccess ? "success-label" : "error-label");
            lblMessage.Visible = true;
        }
    }
}