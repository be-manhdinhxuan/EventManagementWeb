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
    public partial class Event : System.Web.UI.Page
    {
        // Khai báo biến PageIndex và PageSize để quản lý phân trang
        private int CurrentPageIndex
        {
            get { return ViewState["CurrentPageIndex"] != null ? (int)ViewState["CurrentPageIndex"] : 0; }
            set { ViewState["CurrentPageIndex"] = value; }
        }
        private const int PageSize = 8;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // 1. Tải danh sách Địa điểm vào ddlFilterLocation
                LoadLocations();
            }
                // 2. Tải danh sách sự kiện lần đầu
                LoadEventData();
        }

        // Lấy ảnh sự kiện, nếu không có thì trả về ảnh mặc định
        public string GetEventImage(object imageUrlObj)
        {
            string imageUrl = imageUrlObj?.ToString() ?? "";

            if (string.IsNullOrEmpty(imageUrl))
            {
                return "../Assets/images/default-event.jpg";
            }

            // Kiểm tra file có tồn tại không 
            string physicalPath = Server.MapPath("~/Uploads/" + imageUrl);
            if (System.IO.File.Exists(physicalPath))
            {
                return "../Uploads/" + imageUrl;
            }

            // Nếu file không tồn tại → dùng default
            return "../Assets/images/default-event.jpg";
        }

        // Trả về class CSS cho badge
        public string GetEventStatusClass(object statusObj, object startTimeObj, object endTimeObj, object currentReg, object maxCapacity, object deadlineObj)
        {
            string status = statusObj?.ToString() ?? "Draft";
            if (status == "Draft") return "badge--hidden"; // Ẩn hoàn toàn

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
            if (deadline < now || (start > now && deadline >= now == false)) return "badge--primary"; // Sắp diễn ra
            return "badge--success"; // Đang mở đăng ký
        }

        // Trả về text hiển thị trong badge
        public string GetEventStatusText(object statusObj, object startTimeObj, object endTimeObj, object currentReg, object maxCapacity, object deadlineObj)
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

        // Phương thức chung để tải và bind dữ liệu sự kiện
        private void LoadEventData()
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;

            // Xây dựng điều kiện lọc
            string filterCondition = BuildFilterCondition();

            // Tính toán OFFSET và LIMIT cho phân trang
            int offset = CurrentPageIndex * PageSize;

            string sql = $@"
            SELECT SQL_CALC_FOUND_ROWS 
            Id, Title, StartTime, Location, ImageUrl,
            Status, EndTime, CurrentRegistrations, MaxCapacity, RegistrationDeadline -- BỔ SUNG CÁC CỘT NÀY
            FROM Events 
            WHERE IsDeleted = 0 {filterCondition}
            ORDER BY StartTime DESC
            LIMIT {PageSize} OFFSET {offset};
        
            SELECT FOUND_ROWS() AS TotalRecords;"; // Lấy tổng số bản ghi trước khi LIMIT/OFFSET

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlDataAdapter adapter = new MySqlDataAdapter(sql, conn))
                {
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);

                    if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                    {
                        rptEventList.DataSource = ds.Tables[0];
                        rptEventList.DataBind();
                        pnlNoEvents.Visible = false;

                        // Xử lý phân trang
                        int totalRecords = Convert.ToInt32(ds.Tables[1].Rows[0]["TotalRecords"]);
                        SetupPagination(totalRecords);
                    }
                    else
                    {
                        rptEventList.DataSource = null;
                        rptEventList.DataBind();
                        pnlNoEvents.Visible = true;
                        SetupPagination(0);
                    }
                }
            }
        }

        // Xây dựng chuỗi WHERE dựa trên các bộ lọc
        private string BuildFilterCondition()
        {
            // MẶC ĐỊNH: Chỉ hiển thị sự kiện đã Published
            string condition = " AND Status = 'Published'";

            // 1. Tìm kiếm theo tên
            string searchTerm = txtSearchEvent.Text.Trim();
            if (!string.IsNullOrEmpty(searchTerm))
            {
                condition += $" AND Title LIKE '%{MySqlHelper.EscapeString(searchTerm)}%'";
            }

            // 2. Lọc theo Thời gian
            string timeValue = ddlFilterTime.SelectedValue;
            if (timeValue != "ALL")
            {
                switch (timeValue)
                {
                    case "PAST":
                        condition += " AND EndTime < NOW()";
                        break;
                    case "TODAY":
                        condition += " AND DATE(StartTime) = CURDATE()";
                        break;
                    case "THIS_MONTH":
                        condition += " AND YEAR(StartTime) = YEAR(NOW()) AND MONTH(StartTime) = MONTH(NOW())";
                        break;
                    default: // UPCOMING hoặc ALL nhưng không PAST
                        condition += " AND StartTime > NOW()";
                        break;
                }
            }

            // 3. Lọc theo Địa điểm
            string locationValue = ddlFilterLocation.SelectedValue;
            if (locationValue != "ALL" && !string.IsNullOrEmpty(locationValue))
            {
                condition += $" AND Location = '{MySqlHelper.EscapeString(locationValue)}'";
            }

            // 4. Lọc theo Trạng thái mở rộng
            string statusValue = ddlFilterStatus.SelectedValue;
            if (statusValue != "ALL")
            {
                switch (statusValue)
                {
                    case "OPEN":
                        condition += " AND StartTime > NOW() AND CurrentRegistrations < MaxCapacity AND (RegistrationDeadline IS NULL OR RegistrationDeadline >= NOW())";
                        break;
                    case "FULL":
                        condition += " AND StartTime > NOW() AND CurrentRegistrations >= MaxCapacity";
                        break;
                    case "UPCOMING":
                        condition += " AND StartTime > NOW()";
                        break;
                    case "PAST":
                        condition = condition.Replace("Status = 'Published'", "Status IN ('Published', 'Completed')"); // Cho phép xem Completed
                        condition += " AND EndTime < NOW()";
                        break;
                }
            }

            return condition;
        }

        // Phương thức tải các tùy chọn địa điểm
        private void LoadLocations()
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;

            // SQL lấy các địa điểm duy nhất, không rỗng và chưa bị xóa
            string sql = "SELECT DISTINCT Location FROM Events WHERE Location IS NOT NULL AND Location != '' AND IsDeleted = 0 ORDER BY Location ASC";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    conn.Open();
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        // Xóa các mục cũ (nếu có)
                        ddlFilterLocation.Items.Clear();

                        // Thêm mục mặc định đầu tiên
                        ddlFilterLocation.Items.Add(new ListItem("Tất cả", "ALL"));

                        while (reader.Read())
                        {
                            string loc = reader["Location"].ToString();
                            // Thêm địa điểm từ DB vào DropDownList
                            ddlFilterLocation.Items.Add(new ListItem(loc, loc));
                        }
                    }
                }
            }
        }

        // --- Xử lý sự kiện ---

        // Xử lý khi giá trị DropDownList thay đổi (AutoPostBack)
        protected void ddlFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            CurrentPageIndex = 0; // Đặt lại về trang 1
            LoadEventData();
        }

        // Xử lý khi nội dung ô tìm kiếm thay đổi (AutoPostBack)
        protected void txtSearchEvent_TextChanged(object sender, EventArgs e)
        {
            CurrentPageIndex = 0; // Đặt lại về trang 1
            LoadEventData();
        }

        // Xử lý nút Đặt lại bộ lọc
        protected void btnResetFilter_Click(object sender, EventArgs e)
        {
            txtSearchEvent.Text = string.Empty;
            ddlFilterTime.SelectedValue = "ALL";
            ddlFilterLocation.SelectedValue = "ALL";
            ddlFilterStatus.SelectedValue = "ALL";
            CurrentPageIndex = 0;
            LoadEventData();
        }

        // Xử lý chuyển trang tiếp theo
        protected void btnNextPage_Click(object sender, EventArgs e)
        {
            CurrentPageIndex++;
            LoadEventData();
        }

        // Xử lý chuyển trang trước
        protected void btnPrevPage_Click(object sender, EventArgs e)
        {
            if (CurrentPageIndex > 0)
            {
                CurrentPageIndex--;
                LoadEventData();
            }
        }

        // --- Xử lý Phân trang (Pagination) ---

        private void SetupPagination(int totalRecords)
        {
            int totalPages = (int)Math.Ceiling((double)totalRecords / PageSize);

            // Nếu chỉ có 1 trang hoặc không có dữ liệu, ẩn phân trang (tùy chọn)
            if (totalPages <= 1)
            {
                phPagination.Visible = false;
                btnPrevPage.Visible = false;
                btnNextPage.Visible = false;
                return;
            }

            phPagination.Visible = true;
            btnPrevPage.Visible = true;
            btnNextPage.Visible = true;

            // Xóa các nút cũ trước khi tạo mới
            phPagination.Controls.Clear();

            
            // Đảm bảo luôn hiển thị 5 nút xung quanh trang hiện tại
            int maxButtons = 5;
            int startPage = CurrentPageIndex + 1 - 2;
            int endPage = CurrentPageIndex + 1 + 2;

            if (startPage <= 0)
            {
                endPage -= (startPage - 1);
                startPage = 1;
            }

            if (endPage > totalPages)
            {
                endPage = totalPages;
                if (endPage - maxButtons + 1 > 0)
                {
                    startPage = endPage - maxButtons + 1;
                }
                else
                {
                    startPage = 1;
                }
            }

            for (int i = startPage; i <= endPage; i++)
            {
                LinkButton pageButton = new LinkButton();
                pageButton.ID = "page_" + i;
                pageButton.Text = i.ToString();
                pageButton.CommandArgument = (i - 1).ToString();
                pageButton.Click += new EventHandler(PageButton_Click);

                // CSS chuẩn BEM
                string activeClass = (i - 1 == CurrentPageIndex) ? " pagination__button--active" : "";
                pageButton.CssClass = "pagination__button" + activeClass;

                phPagination.Controls.Add(pageButton);
            }

            // Cập nhật trạng thái nút điều hướng
            btnPrevPage.Enabled = CurrentPageIndex > 0;
            btnNextPage.Enabled = CurrentPageIndex < totalPages - 1;

            // Thêm class 'disabled' cho CSS nếu nút bị vô hiệu hóa
            btnPrevPage.CssClass = "pagination__button" + (CurrentPageIndex == 0 ? " pagination__button--disabled" : "");
            btnNextPage.CssClass = "pagination__button" + (CurrentPageIndex >= totalPages - 1 ? " pagination__button--disabled" : "");
        }

        // Xử lý khi nhấn vào nút trang cụ thể
        protected void PageButton_Click(object sender, EventArgs e)
        {
            LinkButton btn = (LinkButton)sender;
            CurrentPageIndex = Convert.ToInt32(btn.CommandArgument);
            LoadEventData();
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