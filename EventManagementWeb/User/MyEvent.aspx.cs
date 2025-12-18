using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace EventManagementWeb.User
{
    public partial class MyEvent : Page
    {
        private int GetUserId()
        {
            if (Session["UserId"] == null)
            {
                Response.Redirect("~/Account/Login.aspx");
                Context.ApplicationInstance.CompleteRequest();
                return 0;
            }
            return Convert.ToInt32(Session["UserId"]);
        }

        private int CurrentPageIndex
        {
            get { return ViewState["CurrentPageIndex"] != null ? (int)ViewState["CurrentPageIndex"] : 0; }
            set { ViewState["CurrentPageIndex"] = value; }
        }

        private const int PageSize = 8;

        private string CurrentTab
        {
            get => ViewState["CurrentTab"]?.ToString() ?? "upcoming";
            set => ViewState["CurrentTab"] = value;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["UserId"] == null)
            {
                Response.Redirect("~/Account/Login.aspx");
                return;
            }

            if (!IsPostBack)
            {
                CurrentTab = "upcoming";
                CurrentPageIndex = 0;

                LoadLocations();
                LoadEventData();
            }
        }

        public string GetEventImage(object imageUrlObj)
        {
            string imageUrl = imageUrlObj?.ToString() ?? "";
            if (string.IsNullOrEmpty(imageUrl))
                return "../Assets/images/default-event.jpg";

            string physicalPath = Server.MapPath("~/Uploads/" + imageUrl);
            return System.IO.File.Exists(physicalPath) ? "../Uploads/" + imageUrl : "../Assets/images/default-event.jpg";
        }

        public string GetEventStatusClass(object statusObj, object startTimeObj, object endTimeObj,
            object currentReg, object maxCapacity, object deadlineObj)
        {
            string status = statusObj?.ToString() ?? "Draft";
            if (status == "Draft") return "badge--hidden";

            DateTime start = startTimeObj != DBNull.Value ? Convert.ToDateTime(startTimeObj) : DateTime.MaxValue;
            DateTime end = endTimeObj != DBNull.Value ? Convert.ToDateTime(endTimeObj) : DateTime.MaxValue;
            DateTime deadline = deadlineObj != DBNull.Value ? Convert.ToDateTime(deadlineObj) : DateTime.MaxValue;
            int current = currentReg != DBNull.Value ? Convert.ToInt32(currentReg) : 0;
            int max = maxCapacity != DBNull.Value ? Convert.ToInt32(maxCapacity) : 1;
            DateTime now = DateTime.Now;

            if (status == "Cancelled") return "badge--danger";
            if (status == "Completed" || end < now) return "badge--secondary";
            if (start <= now && now <= end) return "badge--info";
            if (current >= max) return "badge--warning";
            if (deadline < now || start <= now) return "badge--primary";
            return "badge--success";
        }

        public string GetEventStatusText(object statusObj, object startTimeObj, object endTimeObj,
            object currentReg, object maxCapacity, object deadlineObj)
        {
            string status = statusObj?.ToString() ?? "Draft";
            if (status == "Draft") return "";

            DateTime start = startTimeObj != DBNull.Value ? Convert.ToDateTime(startTimeObj) : DateTime.MaxValue;
            DateTime end = endTimeObj != DBNull.Value ? Convert.ToDateTime(endTimeObj) : DateTime.MaxValue;
            DateTime deadline = deadlineObj != DBNull.Value ? Convert.ToDateTime(deadlineObj) : DateTime.MaxValue;
            int current = currentReg != DBNull.Value ? Convert.ToInt32(currentReg) : 0;
            int max = maxCapacity != DBNull.Value ? Convert.ToInt32(maxCapacity) : 1;
            DateTime now = DateTime.Now;

            if (status == "Cancelled") return "Đã hủy";
            if (status == "Completed" || end < now) return "Đã kết thúc";
            if (start <= now && now <= end) return "Đang diễn ra";
            if (current >= max) return "Đã đầy";
            if (deadline < now || start <= now) return "Sắp diễn ra";
            return "Đang mở đăng ký";
        }

        private void LoadEventData()
        {
            int userId = GetUserId();
            int offset = CurrentPageIndex * PageSize;
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;

            string tabFilter = "";
            switch (CurrentTab)
            {
                case "upcoming":
                    tabFilter = " AND er.Status = 'Approved' AND e.StartTime > NOW()";
                    break;
                case "attended":
                    tabFilter = " AND er.Status = 'Approved' AND e.EndTime < NOW()";
                    break;
                case "cancelled":
                    tabFilter = " AND er.Status = 'Cancelled'";
                    break;
                default:
                    tabFilter = "";
                    break;
            }

            string commonFilter = BuildFilterCondition();

            string sql = $@"
                SELECT SQL_CALC_FOUND_ROWS
                    e.Id, e.Title, e.StartTime, e.EndTime, e.Location, e.ImageUrl,
                    e.Status, e.CurrentRegistrations, e.MaxCapacity, e.RegistrationDeadline
                FROM EventRegistrations er
                JOIN Events e ON er.EventId = e.Id
                WHERE er.UserId = @userId AND er.IsDeleted = 0 AND e.IsDeleted = 0
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
                    cmd.Parameters.AddWithValue("@offset", offset);

                    
                    string searchTerm = txtSearchEvent.Text.Trim();
                    if (!string.IsNullOrEmpty(searchTerm))
                        cmd.Parameters.AddWithValue("@search", "%" + searchTerm + "%");

                    string location = ddlFilterLocation.SelectedValue;
                    if (location != "ALL")
                        cmd.Parameters.AddWithValue("@location", location);

                    conn.Open();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        DataSet ds = new DataSet();
                        adapter.Fill(ds);

                        if (ds.Tables[0].Rows.Count > 0)
                        {
                            rptEventList.DataSource = ds.Tables[0];
                            rptEventList.DataBind();
                            pnlNoEvents.CssClass = "no-events no-events--hidden";

                            int totalRecords = Convert.ToInt32(ds.Tables[1].Rows[0]["TotalRecords"]);
                            SetupPagination(totalRecords);
                        }
                        else
                        {
                            rptEventList.DataSource = null;
                            rptEventList.DataBind();
                            pnlNoEvents.CssClass = "no-events";

                            SetupPagination(0);
                        }
                    }
                }
            }
        }

        private void UpdateTabUI()
        {
            liTabUpcoming.Attributes["class"] = "events-tabs__item";
            liTabAttended.Attributes["class"] = "events-tabs__item";
            liTabCancelled.Attributes["class"] = "events-tabs__item";

            switch (CurrentTab)
            {
                case "upcoming":
                    liTabUpcoming.Attributes["class"] += " events-tabs__item--active";
                    break;
                case "attended":
                    liTabAttended.Attributes["class"] += " events-tabs__item--active";
                    break;
                case "cancelled":
                    liTabCancelled.Attributes["class"] += " events-tabs__item--active";
                    break;
            }
        }


        private string BuildFilterCondition()
        {
            List<string> conditions = new List<string>();

            // 1. Tìm kiếm theo tên
            string searchTerm = txtSearchEvent.Text.Trim();
            if (!string.IsNullOrEmpty(searchTerm))
                conditions.Add(" e.Title LIKE @search ");

            // 2. Lọc theo thời gian 
            string timeValue = ddlFilterTime.SelectedValue;
            if (timeValue != "ALL")
            {
                string timeCond = "";
                switch (timeValue)
                {
                    case "PAST":
                        timeCond = " e.EndTime < NOW() ";
                        break;
                    case "TODAY":
                        timeCond = " DATE(e.StartTime) = CURDATE() ";
                        break;
                    case "THIS_MONTH":
                        timeCond = " YEAR(e.StartTime) = YEAR(NOW()) AND MONTH(e.StartTime) = MONTH(NOW()) ";
                        break;
                    default:
                        timeCond = " e.StartTime > NOW() ";
                        break;
                }
                conditions.Add(timeCond);
            }

            // 3. Lọc theo địa điểm
            string location = ddlFilterLocation.SelectedValue;
            if (location != "ALL")
                conditions.Add(" e.Location = @location ");

            // 4. Lọc theo trạng thái
            string statusValue = ddlFilterStatus.SelectedValue;
            if (statusValue != "ALL")
            {
                string statusCond = "";
                switch (statusValue)
                {
                    case "OPEN":
                        statusCond = " e.StartTime > NOW() AND e.CurrentRegistrations < e.MaxCapacity AND (e.RegistrationDeadline IS NULL OR e.RegistrationDeadline >= NOW()) ";
                        break;
                    case "FULL":
                        statusCond = " e.StartTime > NOW() AND e.CurrentRegistrations >= e.MaxCapacity ";
                        break;
                    case "UPCOMING":
                        statusCond = " e.StartTime > NOW() ";
                        break;
                    case "PAST":
                        statusCond = " e.EndTime < NOW() ";
                        break;
                }
                if (!string.IsNullOrEmpty(statusCond))
                    conditions.Add(statusCond);
            }

            // Trả về chuỗi kết hợp các điều kiện bằng từ khóa AND
            return conditions.Count > 0 ? " AND " + string.Join(" AND ", conditions) : "";
        }

        private void LoadLocations()
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = "SELECT DISTINCT Location FROM Events WHERE Location IS NOT NULL AND Location != '' AND IsDeleted = 0 ORDER BY Location";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    conn.Open();
                    ddlFilterLocation.Items.Clear();
                    ddlFilterLocation.Items.Add(new ListItem("Tất cả địa điểm", "ALL"));

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string loc = reader["Location"].ToString();
                            ddlFilterLocation.Items.Add(new ListItem(loc, loc));
                        }
                    }
                }
            }
        }

        private void LoadTabCounts()
        {
            int userId = GetUserId();
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
                            litCountUpcoming.Text = r["upcoming"].ToString();
                            litCountAttended.Text = r["attended"].ToString();
                            litCountCancelled.Text = r["cancelled"].ToString();
                        }
                        else
                        {
                            litCountUpcoming.Text = litCountAttended.Text = litCountCancelled.Text = "0";
                        }
                    }
                }
            }
        }

        protected void Tab_Click(object sender, EventArgs e)
        {
            LinkButton btn = (LinkButton)sender;

            CurrentTab = btn.CommandArgument;
            CurrentPageIndex = 0;

            LoadEventData();
        }


        protected void ddlFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            CurrentPageIndex = 0;
            LoadEventData();
        }

        protected void txtSearchEvent_TextChanged(object sender, EventArgs e)
        {
            CurrentPageIndex = 0;
            LoadEventData();
        }

        protected void btnResetFilter_Click(object sender, EventArgs e)
        {
            txtSearchEvent.Text = "";
            ddlFilterTime.SelectedValue = "ALL";
            ddlFilterLocation.SelectedValue = "ALL";
            ddlFilterStatus.SelectedValue = "ALL";
            CurrentPageIndex = 0;
            LoadEventData();
        }

        protected void btnPrevPage_Click(object sender, EventArgs e)
        {
            if (CurrentPageIndex > 0) CurrentPageIndex--;
            LoadEventData();
        }

        protected void btnNextPage_Click(object sender, EventArgs e)
        {
            CurrentPageIndex++;
            LoadEventData();
        }

        private void SetupPagination(int totalRecords)
        {
            int totalPages = totalRecords > 0 ? (int)Math.Ceiling((double)totalRecords / PageSize) : 1;

            phPagination.Controls.Clear();

            if (totalPages <= 1)
            {
                phPagination.Visible = btnPrevPage.Visible = btnNextPage.Visible = false;
                return;
            }

            phPagination.Visible = btnPrevPage.Visible = btnNextPage.Visible = true;

            int maxButtons = 5;
            int startPage = Math.Max(1, CurrentPageIndex + 1 - 2);
            int endPage = Math.Min(totalPages, startPage + maxButtons - 1);
            if (endPage - startPage + 1 < maxButtons)
                startPage = Math.Max(1, endPage - maxButtons + 1);

            for (int i = startPage; i <= endPage; i++)
            {
                LinkButton btn = new LinkButton
                {
                    Text = i.ToString(),
                    CommandArgument = (i - 1).ToString(),
                    CssClass = "pagination__button" + ((i - 1 == CurrentPageIndex) ? " pagination__button--active" : "")
                };
                btn.Click += PageButton_Click;
                phPagination.Controls.Add(btn);
            }

            btnPrevPage.Enabled = CurrentPageIndex > 0;
            btnNextPage.Enabled = CurrentPageIndex < totalPages - 1;
        }

        protected void PageButton_Click(object sender, EventArgs e)
        {
            CurrentPageIndex = Convert.ToInt32(((LinkButton)sender).CommandArgument);
            LoadEventData();
        }

        protected void Page_PreRender(object sender, EventArgs e)
        {
            LoadTabCounts();
            UpdateTabUI();
        }

        protected void Logout_Click(object sender, EventArgs e)
        {
            Session.Clear();
            Session.Abandon();
            FormsAuthentication.SignOut();
            Response.Redirect("~/Account/Login.aspx");
        }
    }
}