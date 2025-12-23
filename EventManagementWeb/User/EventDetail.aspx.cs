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
            // Kiểm tra đăng nhập
            if (Session["UserId"] == null)
            {
                Response.Redirect("~/Account/Login.aspx", false);
                return;
            }

            int userId = Convert.ToInt32(Session["UserId"]);
            int eventId = 0;
            if (!string.IsNullOrEmpty(Request.QueryString["id"]) && int.TryParse(Request.QueryString["id"], out int parsedId))
            {
                eventId = parsedId;
            }
            else
            {
                Response.Redirect("Event.aspx", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            // Xử lý POST request (đăng ký/hủy/logout)
            if (Request.HttpMethod == "POST")
            {
                string action = Request.Form["btnAction"];

                if (action == "register")
                {
                    RegisterEvent(userId, eventId);
                    return; // QUAN TRỌNG: Return để ngăn code chạy tiếp
                }
                else if (action == "cancelRegistration")
                {
                    CancelRegistration(userId, eventId);
                    return; // QUAN TRỌNG: Return để ngăn code chạy tiếp
                }
                else if (action == "logout")
                {
                    HandleLogout();
                    return;
                }
            }

            // Load dữ liệu cho trang (chỉ chạy khi không phải POST hoặc sau khi redirect)
            LoadHeaderNotifications(userId);
            LoadUserAvatar(userId);
            LoadEventDetail(eventId, userId);
        }

        private void LoadHeaderNotifications(int userId)
        {
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
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
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
                LEFT JOIN EventRegistrations er ON er.EventId = e.Id
                    AND er.UserId = @userId
                    AND er.Status = 'Approved'
                WHERE e.Id = @eventId
                    AND e.IsDeleted = 0
                    AND e.Status = 'Published'";

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
                            Response.Redirect("Event.aspx", false);
                            Context.ApplicationInstance.CompleteRequest();
                        }
                    }
                }
            }
        }

        private void RegisterEvent(int userId, int eventId)
        {
            string note = Request.Form["note"]?.Trim() ?? "";

            // Kiểm tra điều kiện ban đầu
            LoadEventDetail(eventId, userId);
            if (EventData == null || IsRegistered || IsFull || IsPastDeadline)
            {
                ShowMessage("Không thể đăng ký sự kiện này!");
                return;
            }

            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                using (MySqlTransaction trans = conn.BeginTransaction(IsolationLevel.Serializable)) // Serializable để lock mạnh hơn
                {
                    try
                    {
                        // 1. LOCK ROW Events + kiểm tra lại điều kiện (chống race condition)
                        string lockSql = @"
                    SELECT CurrentRegistrations, MaxCapacity
                    FROM Events
                    WHERE Id = @eventId AND Status = 'Published' AND IsDeleted = 0
                    FOR UPDATE";

                        int currentCount = 0;
                        int maxCapacity = 0;
                        using (MySqlCommand lockCmd = new MySqlCommand(lockSql, conn, trans))
                        {
                            lockCmd.Parameters.AddWithValue("@eventId", eventId);
                            using (MySqlDataReader r = lockCmd.ExecuteReader())
                            {
                                if (!r.Read())
                                {
                                    trans.Rollback();
                                    ShowMessage("Sự kiện không tồn tại hoặc không khả dụng!");
                                    return;
                                }
                                currentCount = r.GetInt32("CurrentRegistrations");
                                maxCapacity = r.GetInt32("MaxCapacity");
                            }
                        }

                        if (currentCount >= maxCapacity)
                        {
                            trans.Rollback();
                            ShowMessage("Sự kiện đã đầy!");
                            return;
                        }

                        // 2. Double-check user chưa đăng ký
                        string checkUserSql = @"
                    SELECT COUNT(*) FROM EventRegistrations
                    WHERE EventId = @eventId AND UserId = @userId AND Status = 'Approved'";

                        using (MySqlCommand checkCmd = new MySqlCommand(checkUserSql, conn, trans))
                        {
                            checkCmd.Parameters.AddWithValue("@eventId", eventId);
                            checkCmd.Parameters.AddWithValue("@userId", userId);
                            if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
                            {
                                trans.Rollback();
                                ShowMessage("Bạn đã đăng ký rồi!");
                                return;
                            }
                        }

                        // 3. Tăng CurrentRegistrations
                        string updateSql = @"UPDATE Events e
                    SET e.CurrentRegistrations = (
                        SELECT COUNT(*)
                        FROM EventRegistrations
                        WHERE EventId = @eventId AND Status = 'Approved'
                    )
                    WHERE e.Id = @eventId";
                        using (MySqlCommand cmd = new MySqlCommand(updateSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@eventId", eventId);
                            cmd.ExecuteNonQuery();
                        }

                        // 4. Insert đăng ký
                        string insertSql = @"INSERT INTO EventRegistrations
                                   (EventId, UserId, Status, Note, CreatedAt)
                                   VALUES (@eventId, @userId, 'Approved', @note, NOW())";

                        using (MySqlCommand cmd = new MySqlCommand(insertSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@eventId", eventId);
                            cmd.Parameters.AddWithValue("@userId", userId);
                            cmd.Parameters.AddWithValue("@note", string.IsNullOrEmpty(note) ? (object)DBNull.Value : note);
                            cmd.ExecuteNonQuery();
                        }

                        // 5. Tạo thông báo (giữ nguyên như cũ)
                        string eventTitle = EventData["Title"]?.ToString() ?? "sự kiện";
                        string userMessage = string.IsNullOrEmpty(note)
                            ? $"Bạn đã đăng ký thành công sự kiện \"{eventTitle}\"."
                            : $"Bạn đã đăng ký thành công sự kiện \"{eventTitle}\" với ghi chú: {note}";

                        // User notification
                        string userNotifSql = @"INSERT INTO Notifications
                                      (UserId, Type, Title, Message, RelatedEventId, CreatedAt)
                                      VALUES (@userId, 'Registration', 'Đăng ký sự kiện', @message, @eventId, NOW())";
                        using (MySqlCommand cmd = new MySqlCommand(userNotifSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@userId", userId);
                            cmd.Parameters.AddWithValue("@message", userMessage);
                            cmd.Parameters.AddWithValue("@eventId", eventId);
                            cmd.ExecuteNonQuery();
                        }

                        // Admin notification
                        string adminMessage = $"Người dùng đã đăng ký sự kiện \"{eventTitle}\".";
                        string adminNotifSql = @"INSERT INTO Notifications
                                       (UserId, Type, Title, Message, RelatedEventId, CreatedAt)
                                       SELECT Id, 'System', 'Có đăng ký mới', @adminMessage, @eventId, NOW()
                                       FROM Users WHERE Role = 'Admin'";
                        using (MySqlCommand cmd = new MySqlCommand(adminNotifSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@adminMessage", adminMessage);
                            cmd.Parameters.AddWithValue("@eventId", eventId);
                            cmd.ExecuteNonQuery();
                        }

                        trans.Commit();

                        ShowMessage("Đăng ký sự kiện thành công!", true);
                        Response.Redirect(Request.RawUrl + "?msg=register_success", true);
                    }
                    catch (Exception ex)
                    {
                        trans.Rollback();
                        System.Diagnostics.Debug.WriteLine("RegisterEvent Error: " + ex.Message);
                        ShowMessage("Lỗi khi đăng ký! Vui lòng thử lại.");
                    }
                }
            }
        }

        private void CancelRegistration(int userId, int eventId)
        {
            string cancellationReason = Request.Form["cancelReason"]?.Trim() ?? "";

            // Kiểm tra điều kiện
            LoadEventDetail(eventId, userId);

            if (EventData == null)
            {
                ShowMessage("Sự kiện không tồn tại!");
                return;
            }

            if (!IsRegistered)
            {
                ShowMessage("Bạn chưa đăng ký sự kiện này!");
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
                        // 1. Update registration status
                        string updateRegSql = @"UPDATE EventRegistrations
                                              SET Status = 'Cancelled',
                                                  CancellationReason = @cancellationReason,
                                                  CancelledAt = NOW()
                                              WHERE EventId = @eventId
                                                AND UserId = @userId
                                                AND Status = 'Approved'";

                        int rowsAffected;
                        using (MySqlCommand cmd = new MySqlCommand(updateRegSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@eventId", eventId);
                            cmd.Parameters.AddWithValue("@userId", userId);
                            cmd.Parameters.AddWithValue("@cancellationReason",
                                string.IsNullOrEmpty(cancellationReason) ? (object)DBNull.Value : cancellationReason);
                            rowsAffected = cmd.ExecuteNonQuery();
                        }

                        if (rowsAffected == 0)
                        {
                            trans.Rollback();
                            ShowMessage("Không tìm thấy đăng ký để hủy!");
                            return;
                        }

                        // 2. Update event count
                        string updateEventSql = @"UPDATE Events e
                         SET e.CurrentRegistrations = (
                             SELECT COUNT(*)
                             FROM EventRegistrations
                             WHERE EventId = @eventId AND Status = 'Approved'
                         )
                         WHERE e.Id = @eventId";

                        using (MySqlCommand cmd = new MySqlCommand(updateEventSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@eventId", eventId);
                            cmd.ExecuteNonQuery();
                        }

                        // 3. Tạo thông báo cho User
                        string eventTitle = EventData["Title"]?.ToString() ?? "sự kiện";
                        string userMessage = string.IsNullOrEmpty(cancellationReason)
                            ? $"Bạn đã hủy đăng ký sự kiện \"{eventTitle}\"."
                            : $"Bạn đã hủy đăng ký sự kiện \"{eventTitle}\" với lý do: {cancellationReason}";

                        string userNotifSql = @"INSERT INTO Notifications
                                              (UserId, Type, Title, Message, RelatedEventId, CreatedAt)
                                              VALUES (@userId, 'Registration', 'Hủy đăng ký', @message, @eventId, NOW())";

                        using (MySqlCommand cmd = new MySqlCommand(userNotifSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@userId", userId);
                            cmd.Parameters.AddWithValue("@message", userMessage);
                            cmd.Parameters.AddWithValue("@eventId", eventId);
                            cmd.ExecuteNonQuery();
                        }

                        // 4. Tạo thông báo cho Admin
                        string adminMessage = $"Người dùng đã hủy đăng ký sự kiện \"{eventTitle}\".";
                        string adminNotifSql = @"INSERT INTO Notifications
                                               (UserId, Type, Title, Message, RelatedEventId, CreatedAt)
                                               SELECT Id, 'System', 'Có người hủy đăng ký', @adminMessage, @eventId, NOW()
                                               FROM Users WHERE Role = 'Admin'";

                        using (MySqlCommand cmd = new MySqlCommand(adminNotifSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@adminMessage", adminMessage);
                            cmd.Parameters.AddWithValue("@eventId", eventId);
                            cmd.ExecuteNonQuery();
                        }

                        // 5. Commit transaction
                        trans.Commit();

                        // 6. Redirect về cùng trang (PRG pattern)
                        string redirectUrl = $"EventDetail.aspx?id={eventId}&msg=cancel_success";
                        Response.Redirect(redirectUrl, false);
                        Context.ApplicationInstance.CompleteRequest();
                    }
                    catch (Exception ex)
                    {
                        trans.Rollback();
                        System.Diagnostics.Debug.WriteLine("CancelRegistration Error: " + ex.Message);
                        ShowMessage("Lỗi khi hủy đăng ký! Vui lòng thử lại.");
                    }
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
            message = message.Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "");
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