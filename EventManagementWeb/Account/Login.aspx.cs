using System;
using System.Web.UI;
using MySql.Data.MySqlClient;
using System.Configuration;
using BCrypt.Net;
using System.Web.Security;
using System.Collections.Generic;

namespace EventManagementWeb.Account
{
    public partial class Login : Page
    {

        public string ErrorMessage = "";
        private const string ToastSessionKey = "ToastMessage";


        private void SetToast(string title, string message, string type)
        {

            Session[ToastSessionKey] = new System.Collections.Generic.Dictionary<string, string>
            {
                { "Title", title },
                { "Message", message },
                { "Type", type }
            };
        }
        protected void Page_Load(object sender, EventArgs e)
        {
            // Kiểm tra trạng thái đăng nhập hệ thống
            if (Session["UserId"] != null && User.Identity.IsAuthenticated)
            {
                Response.Redirect("~/Admin/Dashboard.aspx");
            }

            // Kiểm tra nếu người dùng thực hiện hành động POST dữ liệu từ Form
            if (Request.HttpMethod == "POST" && Request.Form["btnAction"] == "login")
            {
                HandleLogin();
            }
        }

        private void HandleLogin()
        {
            string email = Request.Form["email"]?.Trim();
            string password = Request.Form["password"];

            // Kiểm tra tính hợp lệ của dữ liệu đầu vào
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ErrorMessage = "Vui lòng nhập đầy đủ email và mật khẩu";
                return;
            }

            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                try
                {
                    conn.Open();
                    // Truy vấn dữ liệu từ MySQL
                    string sql = "SELECT Id, FullName, Password, Role FROM Users WHERE Email = @email AND IsActive = 1 AND IsDeleted = 0 LIMIT 1";

                    using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                    {
                        // Truyền tham số an toàn vào câu lệnh SQL
                        cmd.Parameters.AddWithValue("@email", email);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string hashedPassword = reader["Password"].ToString();

                                // Kiểm tra mật khẩu bằng thư viện BCrypt
                                if (BCrypt.Net.BCrypt.Verify(password, hashedPassword))
                                {
                                    // Lưu thông tin vào Session để quản lý trạng thái đăng nhập
                                    Session["UserId"] = reader["Id"];
                                    Session["FullName"] = reader["FullName"];
                                    Session["Email"] = email;
                                    Session["Role"] = reader["Role"].ToString();

                                    // Tạo Cookie xác thực của ASP.NET
                                    FormsAuthentication.SetAuthCookie(email, false);
                                    SetToast("Thành công!", "Đăng nhập thành công.", "success");

                                    // Điều hướng dựa trên quyền hạn (Role)
                                    if (Session["Role"].ToString() == "Admin")
                                        Response.Redirect("~/Admin/Dashboard.aspx");
                                    else
                                        Response.Redirect("~/User/Home.aspx");
                                }
                                else
                                {
                                    ErrorMessage = "Mật khẩu không đúng";
                                }
                            }
                            else
                            {
                                ErrorMessage = "Email không tồn tại hoặc tài khoản bị khóa";
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorMessage = "Lỗi hệ thống: " + ex.Message;
                }
            }
        }
    }
}