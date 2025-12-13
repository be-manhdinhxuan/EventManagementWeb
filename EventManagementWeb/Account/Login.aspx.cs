using System;
using System.Web.UI;
using MySql.Data.MySqlClient;
using System.Configuration;
using BCrypt.Net;
using System.Web.Security; 

namespace EventManagementWeb.Account
{
    public partial class Login : Page
    {
        private const string ToastSessionKey = "ToastMessage";

        // Phương thức để lưu thông báo vào Session
        private void SetToast(string title, string message, string type)
        {
            // Sử dụng Session để lưu thông tin cần thiết
            Session[ToastSessionKey] = new
            {
                Title = title,
                Message = message,
                Type = type // success, error, warning
            };
        }
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                
                if (Session["UserId"] != null && User.Identity.IsAuthenticated)
                {
                    Response.Redirect("~/Admin/Dashboard.aspx");
                }
            }
        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            string email = txtEmail.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                lblError.Text = "Vui lòng nhập đầy đủ email và mật khẩu";
                return;
            }

            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                try
                {
                    conn.Open();
                    string sql = "SELECT Id, FullName, Password, Role FROM Users WHERE Email = @email AND IsActive = 1 AND IsDeleted = 0 LIMIT 1";
                    using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@email", email);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string hashedPassword = reader["Password"].ToString();

                                if (BCrypt.Net.BCrypt.Verify(password, hashedPassword))
                                {
                                    
                                    Session["UserId"] = reader["Id"];
                                    Session["FullName"] = reader["FullName"];
                                    Session["Email"] = email;
                                    Session["Role"] = reader["Role"].ToString();

                                    
                                    FormsAuthentication.SetAuthCookie(email, false);
                                    SetToast("Thành công!", "Đăng nhập thành công.", "success");

                                    if (Session["Role"].ToString() == "Admin")
                                        Response.Redirect("~/Admin/Dashboard.aspx");
                                    else
                                        Response.Redirect("~/User/Home.aspx");
                                }
                                else
                                {
                                    lblError.Text = "Mật khẩu không đúng";
                                }
                            }
                            else
                            {
                                lblError.Text = "Email không tồn tại hoặc tài khoản bị khóa";
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    lblError.Text = "Lỗi kết nối cơ sở dữ liệu: " + ex.Message;
                }
            }
        }
    }
}