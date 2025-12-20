using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web;
using System.Web.Security;
using System.Web.UI;

namespace EventManagementWeb.User
{
    public partial class Home : Page
    {

        public DataTable UpcomingEvents;
        public DataRow FeaturedEvent;
        private const string ToastSessionKey = "ToastMessage";
        public DataTable HeaderNotificationList;
        public string FullName = "";
        public string Email = "";
        public string Phone = "";
        public int UnreadCount = 0;
        public string UserAvatar = "../Assets/images/avatar.jpg";
        protected void Page_Load(object sender, EventArgs e)
        {
            // Kiểm tra bảo mật (Nhận Session)
            if (Session["UserId"] == null)
            {
                Response.Redirect("~/Account/Login.aspx");
                return;
            }
            int userId = Convert.ToInt32(Session["UserId"]);

            // Xử lý Đăng xuất thủ công qua Request.Form
            if (Request.HttpMethod == "POST" && Request.Form["btnAction"] == "logout")
            {
                HandleLogout();
                return;
            }

            if (!IsPostBack)
            {
                CheckAndShowToast();
                LoadUpcomingEvents(); // Nạp dữ liệu vào biến public UpcomingEvents
                LoadFeaturedEvent(); // Nạp dữ liệu vào biến public FeaturedEvent
            }
            LoadHeaderNotifications(userId);
            LoadUserAvatar(userId);
        }

        private void LoadUpcomingEvents()
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                string sql = @"
                    SELECT
                        e.Id, 
                        e.Title, 
                        e.StartTime, 
                        e.Location, 
                        e.ImageUrl,
                        e.MaxCapacity, 
                        e.CurrentRegistrations,
                        (e.MaxCapacity - e.CurrentRegistrations) AS AvailableSlots,
                        ROUND((e.CurrentRegistrations * 100.0 / e.MaxCapacity), 0) AS RegistrationRate
                    FROM Events e
                    WHERE e.Status = 'Published'
                      AND e.IsDeleted = 0
                      AND e.StartTime > NOW()                          -- Sự kiện chưa diễn ra (sắp tới)
                      AND (e.RegistrationDeadline IS NOT NULL 
                           AND e.RegistrationDeadline < NOW())         -- Đã hết hạn đăng ký
                    ORDER BY e.StartTime ASC
                    LIMIT 6";

                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    conn.Open();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        UpcomingEvents = new DataTable();
                        adapter.Fill(UpcomingEvents);
                    }
                }
            }
        }

        private void LoadFeaturedEvent()
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                string sql = @"
            SELECT Id, Title, Description, ImageUrl
            FROM Events
            WHERE Status = 'Published' 
              AND IsDeleted = 0 
              AND StartTime > NOW()
            ORDER BY CurrentRegistrations DESC, StartTime ASC
            LIMIT 1";
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    conn.Open();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        if (dt.Rows.Count > 0)
                        {
                            FeaturedEvent = dt.Rows[0];
                        }
                    }
                }
            }
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

        public string GetImageUrl(object imgObj)
        {
            string url = imgObj?.ToString();
            return string.IsNullOrEmpty(url) ? "../Assets/images/default-event.jpg" : "../Uploads/" + url;
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

        private void HandleLogout()
        {
            Session.Clear();
            Session.Abandon();
            FormsAuthentication.SignOut();
            Response.Redirect("~/Account/Login.aspx");
        }

        private void CheckAndShowToast()
        {
            if (Session[ToastSessionKey] != null)
            {
                var toastData = Session[ToastSessionKey] as System.Collections.Generic.Dictionary<string, string>;

                if (toastData != null)
                {

                    string title = HttpUtility.JavaScriptStringEncode(toastData["Title"]);
                    string message = HttpUtility.JavaScriptStringEncode(toastData["Message"]);
                    string type = toastData["Type"];


                    string script = $"showToast('{title}', '{message}', '{type}');";


                    ClientScript.RegisterStartupScript(this.GetType(), "ShowToast", script, true);

                    Session.Remove(ToastSessionKey);
                }
            }
        }
    }
}