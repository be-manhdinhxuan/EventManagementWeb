using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace EventManagementWeb.User
{
    public partial class Setting : UserBasePage
    {
        public DataTable HeaderNotificationList;
        public int UnreadCount = 0;
        public string FullName = "";
        public string Email = "";
        public string Phone = "";
        public string UserAvatar = "../Assets/images/avatar.jpg"; // mặc định

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["UserId"] == null)
            {
                Response.Redirect("~/Account/Login.aspx", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            int userId = Convert.ToInt32(Session["UserId"]);

            if (Request.HttpMethod == "POST")
            {
                string action = Request.Form["btnAction"];


                if (action == "updateProfile")
                {
                    UpdateProfile(userId);
                }
                else if (action == "changePassword")
                {
                    ChangePassword(userId);
                }
                else if (action == "logout")
                {
                    HandleLogout();
                    return;
                }
            }
            LoadHeaderNotifications(userId);
            LoadUserAvatar(userId);
            LoadUserProfile(userId);
        }

        private void LoadHeaderNotifications(int userId)
        {
            // Lấy 5 thông báo mới nhất + đếm chưa đọc
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = @"
                SELECT Id, Type, Title, Message, IsRead, CreatedAt, RelatedEventId
                FROM Notifications
                WHERE UserId = @userId
                ORDER BY CreatedAt DESC
                LIMIT 5";

            string countSql = "SELECT COUNT(*) FROM Notifications WHERE UserId = @userId AND IsRead = 0";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                // Đếm chưa đọc
                using (MySqlCommand cmd = new MySqlCommand(countSql, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    UnreadCount = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // Lấy 5 thông báo mới nhất
                using (MySqlCommand cmd = new MySqlCommand(sql.Replace("TOP 5", "LIMIT 5"), conn)) // MySQL dùng LIMIT
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        HeaderNotificationList = new DataTable();
                        adapter.Fill(HeaderNotificationList);
                    }
                }
            }
        }

        private void LoadUserAvatar(int userId)
        {
            try
            {
                string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
                string sql = "SELECT FullName, Email, Phone, Avatar FROM Users WHERE Id = @userId";

                using (MySqlConnection conn = new MySqlConnection(connStr))
                {
                    using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        conn.Open();
                        using (MySqlDataReader r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                FullName = r["FullName"].ToString();
                                Email = r["Email"].ToString();
                                Phone = r["Phone"] == DBNull.Value ? "" : r["Phone"].ToString();

                                string avatar = r["Avatar"].ToString();
                                UserAvatar = string.IsNullOrEmpty(avatar)
                                    ? "../Assets/images/avatar.jpg"
                                    : "../Uploads/avatars/" + avatar;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadUserProfile Error: " + ex.Message);
                throw;
            }
        }

        public string GetRelativeTime(DateTime createdAt)
        {
            TimeSpan span = DateTime.Now - createdAt;

            if (span.TotalMinutes < 1)
                return "Vừa xong";

            if (span.TotalMinutes < 60)
                return $"{(int)span.TotalMinutes} phút trước";

            if (span.TotalHours < 24)
                return $"{(int)span.TotalHours} giờ trước";

            if (span.TotalDays < 7)
                return $"{(int)span.TotalDays} ngày trước";

            if (span.TotalDays < 30)
                return $"{(int)(span.TotalDays / 7)} tuần trước";

            if (span.TotalDays < 365)
                return createdAt.ToString("dd/MM");

            return createdAt.ToString("dd/MM/yyyy");
        }

        private void LoadUserProfile(int userId)
        {
            try
            {
                string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
                string sql = "SELECT FullName, Email, Phone, Avatar FROM Users WHERE Id = @userId";

                using (MySqlConnection conn = new MySqlConnection(connStr))
                {
                    using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        conn.Open();
                        using (MySqlDataReader r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                FullName = r["FullName"].ToString();
                                Email = r["Email"].ToString();
                                Phone = r["Phone"] == DBNull.Value ? "" : r["Phone"].ToString();

                                string avatar = r["Avatar"].ToString();
                                UserAvatar = string.IsNullOrEmpty(avatar)
                                    ? "../Assets/images/avatar.jpg"
                                    : "../Uploads/avatars/" + avatar;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadUserProfile Error: " + ex.Message);
                throw;
            }
        }

        private void UpdateProfile(int userId)
        {
            try
            {
                string fullName = Request.Form["fullname"]?.Trim();
                string email = Request.Form["email"]?.Trim();
                string phone = Request.Form["phone"]?.Trim();

                // Validate dữ liệu cơ bản
                if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email))
                {
                    ShowMessage("Vui lòng nhập đầy đủ họ tên và email!");
                    return;
                }

                string avatarFileName = null;
                string oldAvatar = GetCurrentAvatar(userId);

                // Xử lý upload avatar
                if (Request.Files.Count > 0)
                {
                    HttpPostedFile file = Request.Files["avatar"];

                    // Chỉ xử lý khi có file được chọn
                    if (file != null && file.ContentLength > 0 && !string.IsNullOrEmpty(file.FileName))
                    {
                        System.Diagnostics.Debug.WriteLine("File uploaded: " + file.FileName);
                        System.Diagnostics.Debug.WriteLine("File size: " + file.ContentLength + " bytes");

                        // Kiểm tra extension
                        string ext = Path.GetExtension(file.FileName).ToLower();
                        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png")
                        {
                            ShowMessage("Chỉ chấp nhận file JPG hoặc PNG!");
                            return;
                        }

                        // Kiểm tra kích thước file (giới hạn 5MB)
                        if (file.ContentLength > 5 * 1024 * 1024)
                        {
                            ShowMessage("Kích thước file không được vượt quá 5MB!");
                            return;
                        }

                        try
                        {
                            // Tạo tên file mới
                            avatarFileName = "avatar_" + userId + "_" + Guid.NewGuid().ToString("N") + ext;
                            string uploadPath = Server.MapPath("~/Uploads/avatars/");

                            System.Diagnostics.Debug.WriteLine("Upload path: " + uploadPath);

                            // Tạo thư mục nếu chưa tồn tại
                            if (!Directory.Exists(uploadPath))
                            {
                                Directory.CreateDirectory(uploadPath);
                                System.Diagnostics.Debug.WriteLine("Created directory: " + uploadPath);
                            }

                            // Lưu file mới
                            string fullPath = Path.Combine(uploadPath, avatarFileName);
                            file.SaveAs(fullPath);

                            System.Diagnostics.Debug.WriteLine("File saved: " + fullPath);

                            // Xóa avatar cũ
                            if (!string.IsNullOrEmpty(oldAvatar) && oldAvatar != "avatar.jpg")
                            {
                                string oldPath = Path.Combine(uploadPath, oldAvatar);
                                if (File.Exists(oldPath))
                                {
                                    try
                                    {
                                        File.Delete(oldPath);
                                        System.Diagnostics.Debug.WriteLine("Deleted old avatar: " + oldPath);
                                    }
                                    catch (Exception delEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine("Delete error: " + delEx.Message);
                                    }
                                }
                            }
                        }
                        catch (Exception saveEx)
                        {
                            System.Diagnostics.Debug.WriteLine("Save file error: " + saveEx.Message);
                            ShowMessage("Lỗi khi lưu file: " + saveEx.Message);
                            return;
                        }
                    }
                }

                // Cập nhật database
                string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
                string sql = "UPDATE Users SET FullName = @fullName, Email = @email, Phone = @phone";
                if (avatarFileName != null)
                {
                    sql += ", Avatar = @avatar";
                }
                sql += " WHERE Id = @userId";

                using (MySqlConnection conn = new MySqlConnection(connStr))
                {
                    using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@fullName", fullName);
                        cmd.Parameters.AddWithValue("@email", email);
                        cmd.Parameters.AddWithValue("@phone", string.IsNullOrEmpty(phone) ? (object)DBNull.Value : phone);
                        cmd.Parameters.AddWithValue("@userId", userId);

                        if (avatarFileName != null)
                        {
                            cmd.Parameters.AddWithValue("@avatar", avatarFileName);
                        }

                        conn.Open();
                        int rowsAffected = cmd.ExecuteNonQuery();

                        System.Diagnostics.Debug.WriteLine("Rows affected: " + rowsAffected);

                        if (rowsAffected > 0)
                        {
                            ShowMessage("Cập nhật thông tin thành công!", true);
                        }
                        else
                        {
                            ShowMessage("Không có thay đổi nào được lưu!");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateProfile Error: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("Stack Trace: " + ex.StackTrace);
                ShowMessage("Lỗi cập nhật: " + ex.Message);
            }
        }

        private string GetCurrentAvatar(int userId)
        {
            try
            {
                string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
                string sql = "SELECT Avatar FROM Users WHERE Id = @userId";

                using (MySqlConnection conn = new MySqlConnection(connStr))
                {
                    using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        conn.Open();
                        var result = cmd.ExecuteScalar();
                        return result == DBNull.Value || result == null ? "" : result.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetCurrentAvatar Error: " + ex.Message);
                return "";
            }
        }

        private void ChangePassword(int userId)
        {
            try
            {
                // 1. Thu thập dữ liệu từ form thuần
                string currentPass = Request.Form["currentPassword"]?.Trim() ?? "";
                string newPass = Request.Form["newPassword"]?.Trim() ?? "";
                string confirmPass = Request.Form["confirmPassword"]?.Trim() ?? "";

                // 2. Kiểm tra các trường hợp ngoại lệ (Validation)
                if (string.IsNullOrEmpty(currentPass) || string.IsNullOrEmpty(newPass) || string.IsNullOrEmpty(confirmPass))
                {
                    ShowMessage("Vui lòng nhập đầy đủ các thông tin mật khẩu!");
                    return;
                }

                // Kiểm tra độ dài mật khẩu mới
                if (newPass.Length < 8)
                {
                    ShowMessage("Mật khẩu mới phải có ít nhất 8 ký tự!");
                    return;
                }

                // Kiểm tra mật khẩu mới và xác nhận mật khẩu
                if (newPass != confirmPass)
                {
                    ShowMessage("Xác nhận mật khẩu mới không trùng khớp!");
                    return;
                }

                // Kiểm tra mật khẩu mới có giống mật khẩu cũ không
                if (newPass == currentPass)
                {
                    ShowMessage("Mật khẩu mới không được trùng với mật khẩu hiện tại!");
                    return;
                }

                string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
                string currentHashInDB = "";

                using (MySqlConnection conn = new MySqlConnection(connStr))
                {
                    // 3. Lấy Hash mật khẩu hiện tại từ Database
                    string checkSql = "SELECT Password FROM Users WHERE Id = @userId";
                    using (MySqlCommand cmd = new MySqlCommand(checkSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        conn.Open();
                        object result = cmd.ExecuteScalar();
                        currentHashInDB = result?.ToString() ?? "";
                    }

                    if (string.IsNullOrEmpty(currentHashInDB))
                    {
                        ShowMessage("Lỗi: Không tìm thấy dữ liệu người dùng!");
                        return;
                    }

                    // 4. KIỂM TRA MẬT KHẨU HIỆN TẠI (Sử dụng BCrypt)
                    if (!BCrypt.Net.BCrypt.Verify(currentPass, currentHashInDB))
                    {
                        ShowMessage("Mật khẩu hiện tại không chính xác!");
                        return;
                    }

                    // 5. Hash mật khẩu mới và Cập nhật
                    string newHash = BCrypt.Net.BCrypt.HashPassword(newPass);
                    string updateSql = "UPDATE Users SET Password = @pass WHERE Id = @userId";

                    using (MySqlCommand updateCmd = new MySqlCommand(updateSql, conn))
                    {
                        updateCmd.Parameters.AddWithValue("@pass", newHash);
                        updateCmd.Parameters.AddWithValue("@userId", userId);

                        int rows = updateCmd.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            // Đổi mật khẩu thành công -> Logout để bắt đăng nhập lại với pass mới
                            string successScript = "alert('Đổi mật khẩu thành công! Vui lòng đăng nhập lại.'); window.location='/Account/Login.aspx';";
                            ClientScript.RegisterStartupScript(this.GetType(), "SuccessRedirect", successScript, true);

                            Session.Clear();
                            Session.Abandon();
                            FormsAuthentication.SignOut();
                        }
                        else
                        {
                            ShowMessage("Lỗi: Không thể cập nhật mật khẩu vào hệ thống!");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ChangePassword Error: " + ex.Message);
                ShowMessage("Lỗi hệ thống: " + ex.Message);
            }
        }

        private void ShowMessage(string message, bool isSuccess = false)
        {
            message = message.Replace("'", "\\'").Replace("\n", " ").Replace("\r", "");
            string script = $"alert('{message}');";
            ClientScript.RegisterStartupScript(this.GetType(), "msg" + DateTime.Now.Ticks, script, true);
        }

        private void HandleLogout()
        {
            Session.Clear();
            Session.Abandon();
            FormsAuthentication.SignOut();
            Response.Redirect("~/Account/Login.aspx");
        }
    }
}