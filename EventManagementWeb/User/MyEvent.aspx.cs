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
    public partial class MyEvent : Page
    {
        public string CurrentTab = "upcoming";           // Tab hiện tại
        public string CountUpcoming = "0";
        public string CountAttended = "0";
        public string CountCancelled = "0";

        public DataTable EventList;
        public List<string> Locations = new List<string>();

        public string SearchTerm = "";
        public string SelectedTime = "ALL";
        public string SelectedLocation = "ALL";
        public string SelectedStatus = "ALL";

        public int CurrentPage = 1;
        public int TotalPages = 1;
        private const int PageSize = 8;

        protected void Page_Load(object sender, EventArgs e)
        {
            // Kiểm tra đăng nhập
            if (Session["UserId"] == null)
            {
                Response.Redirect("~/Account/Login.aspx");
                return;
            }

            int userId = Convert.ToInt32(Session["UserId"]);

            // 1. Thu thập dữ liệu từ request 
            if (Request.HttpMethod == "POST")
            {
                // Bộ lọc
                SearchTerm = Request.Form["txtSearch"]?.Trim() ?? "";
                SelectedTime = Request.Form["ddlTime"] ?? "ALL";
                SelectedLocation = Request.Form["ddlLocation"] ?? "ALL";
                SelectedStatus = Request.Form["ddlStatus"] ?? "ALL";

                // Đổi tab
                if (!string.IsNullOrEmpty(Request.Form["tabAction"]))
                {
                    CurrentTab = Request.Form["tabAction"];
                    CurrentPage = 1; // Reset trang khi đổi tab
                }

                // Phân trang
                if (!string.IsNullOrEmpty(Request.Form["pageAction"]))
                {
                    int.TryParse(Request.Form["pageAction"], out CurrentPage);
                }
            }
            else
            {
                
                CurrentPage = 1;
            }

            // 2. Xử lý nút Đặt lại và Đăng xuất 
            string action = Request.Form["btnAction"];
            if (!string.IsNullOrEmpty(action))
            {
                if (action == "logout")
                {
                    HandleLogout();
                    return;
                }
                else if (action == "reset")
                {
                    SearchTerm = "";
                    SelectedTime = "ALL";
                    SelectedLocation = "ALL";
                    SelectedStatus = "ALL";
                    CurrentPage = 1;
                }
            }

            // 3. Load dữ liệu
            LoadTabCounts(userId);
            LoadLocations();
            LoadEventData(userId);
        }

        private void LoadEventData(int userId)
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;

            // Tab filter
            string tabFilter = "";
            if (CurrentTab == "upcoming")
                tabFilter = " AND er.Status = 'Approved' AND e.StartTime > NOW()";
            else if (CurrentTab == "attended")
                tabFilter = " AND er.Status = 'Approved' AND e.EndTime < NOW()";
            else if (CurrentTab == "cancelled")
                tabFilter = " AND er.Status = 'Cancelled'";

            // Common filter (tìm kiếm, thời gian, địa điểm, trạng thái)
            string commonFilter = BuildFilterCondition();

            string sql = $@"
                SELECT SQL_CALC_FOUND_ROWS
                    e.Id, e.Title, e.StartTime, e.EndTime, e.Location, e.ImageUrl,
                    e.Status, e.CurrentRegistrations, e.MaxCapacity, e.RegistrationDeadline
                FROM EventRegistrations er
                JOIN Events e ON er.EventId = e.Id
                WHERE er.UserId = @userId 
                  AND er.IsDeleted = 0 
                  AND e.IsDeleted = 0
                  {tabFilter} {commonFilter}
                ORDER BY e.StartTime DESC
                LIMIT @limit OFFSET @offset;

                SELECT FOUND_ROWS() AS TotalRecords;";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@limit", PageSize);
                    cmd.Parameters.AddWithValue("@offset", (CurrentPage - 1) * PageSize);

                    // Parameter cho search và location
                    if (!string.IsNullOrEmpty(SearchTerm))
                        cmd.Parameters.AddWithValue("@search", "%" + SearchTerm + "%");

                    if (SelectedLocation != "ALL")
                        cmd.Parameters.AddWithValue("@location", SelectedLocation);

                    conn.Open();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        DataSet ds = new DataSet();
                        adapter.Fill(ds);

                        EventList = ds.Tables[0];

                        int totalRecords = ds.Tables[1].Rows.Count > 0
                            ? Convert.ToInt32(ds.Tables[1].Rows[0]["TotalRecords"])
                            : 0;

                        TotalPages = totalRecords > 0
                            ? (int)Math.Ceiling((double)totalRecords / PageSize)
                            : 1;
                    }
                }
            }
        }

        private string BuildFilterCondition()
        {
            List<string> conditions = new List<string>();

            // Tìm kiếm
            if (!string.IsNullOrEmpty(SearchTerm))
                conditions.Add(" e.Title LIKE @search ");

            // Thời gian
            if (SelectedTime == "TODAY")
                conditions.Add(" DATE(e.StartTime) = CURDATE() ");
            else if (SelectedTime == "PAST")
                conditions.Add(" e.EndTime < NOW() ");
            else if (SelectedTime == "THIS_MONTH")
                conditions.Add(" YEAR(e.StartTime) = YEAR(NOW()) AND MONTH(e.StartTime) = MONTH(NOW()) ");

            // Địa điểm
            if (SelectedLocation != "ALL")
                conditions.Add(" e.Location = @location ");

            // Trạng thái 
            if (SelectedStatus == "OPEN")
            {
                conditions.Add(" e.StartTime > NOW() " +
                               " AND (e.RegistrationDeadline IS NULL OR e.RegistrationDeadline >= NOW()) ");
            }
            else if (SelectedStatus == "UPCOMING")
            {
                conditions.Add(" e.StartTime > NOW() " +
                               " AND e.RegistrationDeadline IS NOT NULL " +
                               " AND e.RegistrationDeadline < NOW() ");
            }
            else if (SelectedStatus == "PAST")
            {
                conditions.Add(" e.EndTime < NOW() ");
            }
            // "ALL" không thêm gì

            return conditions.Count > 0 ? " AND " + string.Join(" AND ", conditions) : "";
        }

        private void LoadTabCounts(int userId)
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = @"
                SELECT
                    COUNT(CASE WHEN er.Status = 'Approved' AND e.StartTime > NOW() THEN 1 END) AS upcoming,
                    COUNT(CASE WHEN er.Status = 'Approved' AND e.EndTime < NOW() THEN 1 END) AS attended,
                    COUNT(CASE WHEN er.Status = 'Cancelled' THEN 1 END) AS cancelled
                FROM EventRegistrations er
                JOIN Events e ON er.EventId = e.Id
                WHERE er.UserId = @userId AND er.IsDeleted = 0 AND e.IsDeleted = 0";

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
                            CountUpcoming = r["upcoming"].ToString();
                            CountAttended = r["attended"].ToString();
                            CountCancelled = r["cancelled"].ToString();
                        }
                        else
                        {
                            CountUpcoming = CountAttended = CountCancelled = "0";
                        }
                    }
                }
            }
        }

        private void LoadLocations()
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = "SELECT DISTINCT Location FROM Events WHERE IsDeleted = 0 AND Location IS NOT NULL AND Location != '' ORDER BY Location ASC";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    conn.Open();
                    using (MySqlDataReader r = cmd.ExecuteReader())
                    {
                        Locations.Clear();
                        while (r.Read())
                        {
                            Locations.Add(r["Location"].ToString());
                        }
                    }
                }
            }
        }

        // Hiển thị ảnh
        public string GetEventImage(object url) =>
            string.IsNullOrEmpty(url?.ToString()) ? "../Assets/images/default-event.jpg" : "../Uploads/" + url;

        // Badge class và text
        public string GetEventStatusClass(DataRow row)
        {
            DateTime start = Convert.ToDateTime(row["StartTime"]);
            DateTime end = Convert.ToDateTime(row["EndTime"]);
            DateTime? deadline = row["RegistrationDeadline"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["RegistrationDeadline"]);
            DateTime now = DateTime.Now;

            if (end < now) return "badge--secondary"; // Đã kết thúc
            if (start > now)
            {
                if (deadline == null || deadline >= now) return "badge--success"; // Đang mở đăng ký
                else return "badge--primary"; // Sắp diễn ra
            }
            if (start <= now && now <= end) return "badge--info"; // Đang diễn ra
            return "badge--success";
        }

        public string GetEventStatusText(DataRow row)
        {
            DateTime start = Convert.ToDateTime(row["StartTime"]);
            DateTime end = Convert.ToDateTime(row["EndTime"]);
            DateTime? deadline = row["RegistrationDeadline"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["RegistrationDeadline"]);
            DateTime now = DateTime.Now;

            if (end < now) return "Đã kết thúc";
            if (start > now)
            {
                if (deadline == null || deadline >= now) return "Đang mở đăng ký";
                else return "Sắp diễn ra";
            }
            if (start <= now && now <= end) return "Đang diễn ra";
            return "Đang mở đăng ký";
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