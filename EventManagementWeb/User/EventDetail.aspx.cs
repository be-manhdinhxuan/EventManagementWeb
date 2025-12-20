using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace EventManagementWeb.User
{
    public partial class EventDetail : System.Web.UI.Page
    {
        public DataTable HeaderNotificationList;
        public int UnreadCount = 0;
        public string FullName = "";
        public string Email = "";
        public string Phone = "";
        public string UserAvatar = "../Assets/images/avatar.jpg";
        public DataRow EventData;
        public int AvailableSlots = 0;
        public string RegistrationStatusText = "";
        public bool IsRegistered = false;
        public bool IsFull = false;
        public bool IsPastDeadline = false;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["UserId"] == null)
            {
                Response.Redirect("~/Account/Login.aspx", false);
                return;
            }

            int userId = Convert.ToInt32(Session["UserId"]);
            int eventId = Convert.ToInt32(Request.QueryString["id"] ?? "0");

            if (Request.HttpMethod == "POST")
            {
                string action = Request.Form["btnAction"];
                if (action == "register")
                {
                    RegisterEvent(userId, eventId);
                }
                else if (action == "cancelRegistration")
                {
                    string reason = Request.Form["cancelReason"] ?? "";
                    CancelRegistration(userId, eventId);
                }
                else if (action == "logout")
                {
                    HandleLogout();
                    return;
                }
            }

            LoadHeaderNotifications(userId);
            LoadUserAvatar(userId);
            LoadEventDetail(eventId, userId);
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

        private void LoadEventDetail(int eventId, int userId)
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = @"
        SELECT e.*, 
               (e.MaxCapacity - e.CurrentRegistrations) AS AvailableSlots,
               er.Status AS RegistrationStatus
        FROM Events e
        LEFT JOIN EventRegistrations er ON er.EventId = e.Id AND er.UserId = @userId AND er.IsDeleted = 0
        WHERE e.Id = @eventId AND e.IsDeleted = 0 AND e.Status = 'Published'";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@eventId", eventId);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    conn.Open();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        if (dt.Rows.Count > 0)
                        {
                            EventData = dt.Rows[0];
                            AvailableSlots = Convert.ToInt32(EventData["AvailableSlots"]);
                            IsRegistered = EventData["RegistrationStatus"]?.ToString() == "Approved";

                            IsFull = AvailableSlots <= 0;
                            IsPastDeadline = EventData["RegistrationDeadline"] != DBNull.Value &&
                                            Convert.ToDateTime(EventData["RegistrationDeadline"]) < DateTime.Now;

                            RegistrationStatusText = IsFull ? "Đã đầy" :
                                                    IsPastDeadline ? "Hết hạn đăng ký" :
                                                    "Còn chỗ";
                        }
                        else
                        {
                            // Fix: Redirect ngay và dừng xử lý
                            Response.Redirect("Event.aspx", false);
                            Response.End(); // Dừng hoàn toàn request để tránh render
                            return;
                        }
                    }
                }
            }
        }

        private void RegisterEvent(int userId, int eventId)
        {
            // Lấy note từ form (tùy chọn)
            string note = Request.Form["note"]?.Trim() ?? "";

            // Kiểm tra điều kiện trước khi đăng ký
            LoadEventDetail(eventId, userId); // reload để kiểm tra mới nhất
            if (IsRegistered || IsFull || IsPastDeadline)
            {
                ShowMessage("Không thể đăng ký sự kiện này!");
                return;
            }

            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = @"INSERT INTO EventRegistrations 
                   (EventId, UserId, Status, Note) 
                   VALUES (@eventId, @userId, 'Approved', @note)";
            string updateSql = "UPDATE Events SET CurrentRegistrations = CurrentRegistrations + 1 WHERE Id = @eventId";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                using (MySqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        using (MySqlCommand cmd = new MySqlCommand(sql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@eventId", eventId);
                            cmd.Parameters.AddWithValue("@userId", userId);
                            cmd.Parameters.AddWithValue("@note", string.IsNullOrEmpty(note) ? (object)DBNull.Value : note);
                            cmd.ExecuteNonQuery();
                        }

                        using (MySqlCommand cmd = new MySqlCommand(updateSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@eventId", eventId);
                            cmd.ExecuteNonQuery();
                        }

                        trans.Commit();
                        ShowMessage("Đăng ký sự kiện thành công!", true);

                        string message = string.IsNullOrEmpty(note)
                            ? "Bạn đã đăng ký thành công sự kiện \"" + (EventData?["Title"]?.ToString() ?? "sự kiện") + "\"."
                            : "Bạn đã đăng ký thành công sự kiện \"" + (EventData?["Title"]?.ToString() ?? "sự kiện") + "\" với ghi chú: " + note;

                        CreateNotification(userId, eventId, "Registration", message);
                    }
                    catch (Exception ex)
                    {
                        trans.Rollback();
                        ShowMessage("Lỗi khi đăng ký sự kiện!");
                        System.Diagnostics.Debug.WriteLine("RegisterEvent Error: " + ex.Message);
                    }
                }
            }

            LoadEventDetail(eventId, userId);
        }

        private void CancelRegistration(int userId, int eventId)
        {
            // Lấy lý do hủy từ form (tùy chọn)
            string cancellationReason = Request.Form["cancelReason"]?.Trim() ?? "";

            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;

            // Cập nhật: đánh dấu hủy + đổi Status + lý do + thời gian hủy
            string sql = @"UPDATE EventRegistrations
               SET Status = 'Cancelled',
                   CancellationReason = @cancellationReason,
                   CancelledAt = NOW()
               WHERE EventId = @eventId
                 AND UserId = @userId
                 AND Status = 'Approved'"; // Chỉ hủy nếu đang Approved

            string updateSql = @"UPDATE Events
                         SET CurrentRegistrations = CurrentRegistrations - 1
                         WHERE Id = @eventId AND CurrentRegistrations > 0";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                using (MySqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        int rowsAffected = 0;

                        // 1. Cập nhật EventRegistrations
                        using (MySqlCommand cmd = new MySqlCommand(sql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@eventId", eventId);
                            cmd.Parameters.AddWithValue("@userId", userId);
                            cmd.Parameters.AddWithValue("@cancellationReason",
                                string.IsNullOrEmpty(cancellationReason) ? (object)DBNull.Value : cancellationReason);
                            rowsAffected = cmd.ExecuteNonQuery();
                        }

                        if (rowsAffected == 0)
                        {
                            throw new Exception("Không tìm thấy đăng ký để hủy");
                        }

                        // 2. Giảm số lượng đăng ký
                        using (MySqlCommand cmd = new MySqlCommand(updateSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@eventId", eventId);
                            cmd.ExecuteNonQuery();
                        }

                        trans.Commit();
                        ShowMessage("Hủy đăng ký thành công!", true);

                        LoadEventDetail(eventId, userId);
                        string title = EventData?["Title"]?.ToString() ?? "sự kiện";

                        string message = string.IsNullOrEmpty(cancellationReason)
                            ? $"Bạn đã hủy đăng ký sự kiện \"{title}\"."
                            : $"Bạn đã hủy đăng ký sự kiện \"{title}\" với lý do: {cancellationReason}";

                        CreateNotification(userId, eventId, "Registration", message);
                    }
                    catch (Exception ex)
                    {
                        trans.Rollback();
                        ShowMessage("Lỗi khi hủy đăng ký!");
                        System.Diagnostics.Debug.WriteLine("CancelRegistration Error: " + ex.Message);
                    }
                }
            }

            LoadEventDetail(eventId, userId);
        }

        private void CreateNotification(int userId, int eventId, string type, string message)
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = "INSERT INTO Notifications (UserId, Type, Title, Message, RelatedEventId, CreatedAt) VALUES (@userId, @type, @title, @message, @eventId, NOW())";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@type", type);
                    cmd.Parameters.AddWithValue("@title", type == "Registration" ? "Đăng ký sự kiện" : "Sự kiện");
                    cmd.Parameters.AddWithValue("@message", message);
                    cmd.Parameters.AddWithValue("@eventId", eventId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public string GetEventImage(object url)
        {
            string img = url?.ToString();
            return string.IsNullOrEmpty(img) ? "../Assets/images/default-event.jpg" : "../Uploads/" + img;
        }

        private void ShowMessage(string message, bool isSuccess = false)
        {
            message = message.Replace("'", "\\'");
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