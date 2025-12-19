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
    public partial class Event : Page
    {
        // Các biến public để .aspx truy cập
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

            // 1. Thu thập dữ liệu từ request
            if (Request.HttpMethod == "POST")
            {
                SearchTerm = Request.Form["txtSearch"]?.Trim() ?? "";
                SelectedTime = Request.Form["ddlTime"] ?? "ALL";
                SelectedLocation = Request.Form["ddlLocation"] ?? "ALL";
                SelectedStatus = Request.Form["ddlStatus"] ?? "ALL";

                // Phân trang
                if (!string.IsNullOrEmpty(Request.Form["pageAction"]))
                {
                    int.TryParse(Request.Form["pageAction"], out CurrentPage);
                }
            }
            else
            {
                // Lần đầu vào trang (GET)
                CurrentPage = 1;
            }

            // 2. Xử lý hành động nút
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
            LoadLocations();
            LoadEventData();
        }

        private void LoadEventData()
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                // Xây dựng filter cơ bản
                string filter = " WHERE e.IsDeleted = 0 AND e.Status = 'Published' ";

                // Tìm kiếm
                if (!string.IsNullOrEmpty(SearchTerm))
                    filter += " AND e.Title LIKE @search ";

                // Địa điểm
                if (SelectedLocation != "ALL")
                    filter += " AND e.Location = @loc ";

                // Lọc thời gian
                if (SelectedTime == "TODAY")
                    filter += " AND DATE(e.StartTime) = CURDATE() ";
                else if (SelectedTime == "PAST")
                    filter += " AND e.EndTime < NOW() ";
                else if (SelectedTime == "THIS_MONTH")
                    filter += " AND YEAR(e.StartTime) = YEAR(NOW()) AND MONTH(e.StartTime) = MONTH(NOW()) ";
                else if (SelectedTime == "UPCOMING")
                    filter += " AND e.StartTime > NOW() ";

                // Lọc trạng thái - CHỈ 4 GIÁ TRỊ
                string statusFilter = "";
                if (SelectedStatus == "OPEN")
                {
                    // Đang mở: StartTime > NOW() và deadline >= NOW() (hoặc NULL)
                    statusFilter = " AND e.StartTime > NOW() " +
                                   " AND (e.RegistrationDeadline IS NULL OR e.RegistrationDeadline >= NOW()) ";
                }
                else if (SelectedStatus == "UPCOMING")
                {
                    // Sắp diễn ra: StartTime > NOW() nhưng deadline < NOW()
                    statusFilter = " AND e.StartTime > NOW() " +
                                   " AND e.RegistrationDeadline IS NOT NULL " +
                                   " AND e.RegistrationDeadline < NOW() ";
                }
                else if (SelectedStatus == "PAST")
                {
                    // Đã kết thúc: EndTime < NOW()
                    statusFilter = " AND e.EndTime < NOW() ";
                }
                // "ALL" → không thêm filter trạng thái

                filter += statusFilter;

                conn.Open();

                // Đếm tổng số bản ghi
                string countSql = "SELECT COUNT(*) FROM Events e " + filter;
                using (MySqlCommand countCmd = new MySqlCommand(countSql, conn))
                {
                    AddSqlParameters(countCmd);
                    int totalRecords = Convert.ToInt32(countCmd.ExecuteScalar());
                    TotalPages = (int)Math.Ceiling((double)totalRecords / PageSize);
                    if (TotalPages == 0) TotalPages = 1;
                }

                // Lấy dữ liệu trang hiện tại
                string sql = $@"SELECT e.* FROM Events e {filter}
                                ORDER BY e.StartTime DESC
                                LIMIT @limit OFFSET @offset";

                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    AddSqlParameters(cmd);
                    cmd.Parameters.AddWithValue("@limit", PageSize);
                    cmd.Parameters.AddWithValue("@offset", (CurrentPage - 1) * PageSize);

                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        EventList = new DataTable();
                        adapter.Fill(EventList);
                    }
                }
            }
        }

        private void AddSqlParameters(MySqlCommand cmd)
        {
            if (cmd.CommandText.Contains("@search"))
                cmd.Parameters.AddWithValue("@search", "%" + SearchTerm + "%");
            if (cmd.CommandText.Contains("@loc"))
                cmd.Parameters.AddWithValue("@loc", SelectedLocation);
        }

        private void LoadLocations()
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                string sql = "SELECT DISTINCT Location FROM Events WHERE IsDeleted = 0 AND Location IS NOT NULL AND Location != '' ORDER BY Location ASC";
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
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

        // Hiển thị ảnh
        public string GetEventImage(object url) =>
            string.IsNullOrEmpty(url?.ToString()) ? "../Assets/images/default-event.jpg" : "../Uploads/" + url;

        
        public string GetEventStatusClass(DataRow row)
        {
            DateTime start = Convert.ToDateTime(row["StartTime"]);
            DateTime end = Convert.ToDateTime(row["EndTime"]);
            DateTime? deadline = row["RegistrationDeadline"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["RegistrationDeadline"]);
            DateTime now = DateTime.Now;

            if (end < now)
                return "badge--secondary"; // Đã kết thúc

            if (start > now)
            {
                if (deadline == null || deadline >= now)
                    return "badge--success"; // Đang mở đăng ký
                else
                    return "badge--primary"; // Sắp diễn ra
            }

            if (start <= now && now <= end)
                return "badge--info"; // Đang diễn ra

            return "badge--success";
        }

        public string GetEventStatusText(DataRow row)
        {
            DateTime start = Convert.ToDateTime(row["StartTime"]);
            DateTime end = Convert.ToDateTime(row["EndTime"]);
            DateTime? deadline = row["RegistrationDeadline"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["RegistrationDeadline"]);
            DateTime now = DateTime.Now;

            if (end < now)
                return "Đã kết thúc";

            if (start > now)
            {
                if (deadline == null || deadline >= now)
                    return "Đang mở đăng ký";
                else
                    return "Sắp diễn ra";
            }

            if (start <= now && now <= end)
                return "Đang diễn ra";

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