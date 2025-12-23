using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web.UI;

namespace EventManagementWeb.Admin
{
    public partial class EventRegistrationManagement : Page
    {
        public DataTable RegistrationList;
        public int CurrentPage = 1;
        public int TotalPages = 1;
        public int TotalRecords = 0;
        private const int PageSize = 10;

        public string EventTitle = "Sự kiện";
        public int CurrentRegistrations = 0;
        public int MaxCapacity = 0;

        public string SearchTerm = "";
        public string SelectedStatus = "ALL";

        private int EventId = 0;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["UserId"] == null || Session["Role"]?.ToString() != "Admin")
            {
                Response.Redirect("~/Account/Login.aspx");
                return;
            }

            if (string.IsNullOrEmpty(Request.QueryString["id"]))
            {
                Response.Redirect("EventManagement.aspx", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            EventId = Convert.ToInt32(Request.QueryString["id"]);

            if (Request.HttpMethod == "POST")
            {
                SearchTerm = Request.Form["txtSearch"]?.Trim() ?? "";
                SelectedStatus = Request.Form["ddlStatus"] ?? "ALL";

                if (Request.Form["btnAction"] == "reset")
                {
                    SearchTerm = "";
                    SelectedStatus = "ALL";
                }

                if (!string.IsNullOrEmpty(Request.Form["pageAction"]))
                {
                    int.TryParse(Request.Form["pageAction"], out CurrentPage);
                }
            }

            if (!IsPostBack)
            {
                CurrentPage = 1;
            }

            LoadEventInfo();
            LoadRegistrations();
        }

        private void LoadEventInfo()
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = "SELECT Title, CurrentRegistrations, MaxCapacity FROM Events WHERE Id = @id AND IsDeleted = 0";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", EventId);
                    conn.Open();
                    using (MySqlDataReader r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            EventTitle = r["Title"].ToString();
                            CurrentRegistrations = Convert.ToInt32(r["CurrentRegistrations"]);
                            MaxCapacity = Convert.ToInt32(r["MaxCapacity"]);
                        }
                        else
                        {
                            Response.Redirect("EventManagement.aspx", false);
                            Context.ApplicationInstance.CompleteRequest();
                        }
                    }
                }
            }
        }

        private void LoadRegistrations()
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;

            List<MySqlParameter> filterParams = new List<MySqlParameter>();
            string filter = BuildFilterCondition(ref filterParams);

            string sql = $@"
        SELECT SQL_CALC_FOUND_ROWS
            u.FullName, u.Email, u.Phone, u.Avatar,
            er.UpdatedAt, er.Status, er.Note
        FROM EventRegistrations er
        INNER JOIN Users u ON er.UserId = u.Id
        WHERE er.EventId = @eventId AND er.IsDeleted = 0
          {filter}
        ORDER BY er.UpdatedAt DESC
        LIMIT @limit OFFSET @offset;

        SELECT FOUND_ROWS() AS TotalRecords;";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@eventId", EventId);
                    cmd.Parameters.AddWithValue("@limit", PageSize);
                    cmd.Parameters.AddWithValue("@offset", (CurrentPage - 1) * PageSize);

                    // Thêm các parameter từ filter (search và status)
                    if (filterParams != null)
                    {
                        foreach (var param in filterParams)
                        {
                            cmd.Parameters.Add(param);
                        }
                    }

                    conn.Open();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        DataSet ds = new DataSet();
                        adapter.Fill(ds);

                        RegistrationList = ds.Tables[0];
                        TotalRecords = ds.Tables[1].Rows.Count > 0 ? Convert.ToInt32(ds.Tables[1].Rows[0]["TotalRecords"]) : 0;
                        TotalPages = TotalRecords > 0 ? (int)Math.Ceiling((double)TotalRecords / PageSize) : 1;
                    }
                }
            }
        }

        private string BuildFilterCondition(ref List<MySqlParameter> parameters)
        {
            var conditions = new List<string>();
            parameters = new List<MySqlParameter>();

            if (!string.IsNullOrEmpty(SearchTerm))
            {
                conditions.Add("(u.FullName LIKE @search OR u.Email LIKE @search)");
                parameters.Add(new MySqlParameter("@search", "%" + SearchTerm + "%"));
            }

            if (SelectedStatus != "ALL")
            {
                conditions.Add("er.Status = @status");
                parameters.Add(new MySqlParameter("@status", SelectedStatus));
            }

            return conditions.Count > 0 ? "AND " + string.Join(" AND ", conditions) : "";
        }

        public string GetUserAvatar(object avatar)
        {
            string avt = avatar?.ToString();
            return string.IsNullOrEmpty(avt)
                ? "../Assets/images/avatar.jpg"
                : "../Uploads/avatars/" + avt;
        }

        public string GetStatusClass(string status)
        {
            switch (status)
            {
                case "Approved": return "badge--success";
                case "Cancelled": return "badge--cancelled";
                case "Pending": return "badge--processing";
                default: return "badge--draft";
            }
        }

        public string GetStatusText(string status)
        {
            switch (status)
            {
                case "Approved": return "Xác nhận";
                case "Cancelled": return "Đã hủy";
                case "Pending": return "Đang xử lý";
                default: return status;
            }
        }
    }
}