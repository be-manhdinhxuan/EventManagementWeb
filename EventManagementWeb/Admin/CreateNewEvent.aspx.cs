using MySql.Data.MySqlClient;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;

namespace EventManagementWeb.Admin
{
    public partial class CreateNewEvent : Page
    {
        public string PageTitle = "Tạo sự kiện mới";

        public string CurrentTitle = "";
        public string CurrentDescription = "";
        public string CurrentStartTime = "";
        public string CurrentEndTime = "";
        public string CurrentLocation = "";
        public string CurrentMaxCapacity = "";
        public string CurrentRegistrationDeadline = "";
        public string CurrentStatus = "Draft";

        private int EventId = 0; // 0 = thêm mới

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["UserId"] == null || Session["Role"]?.ToString() != "Admin")
            {
                Response.Redirect("~/Account/Login.aspx");
                return;
            }

            if (!string.IsNullOrEmpty(Request.QueryString["edit"]))
            {
                EventId = Convert.ToInt32(Request.QueryString["edit"]);
                PageTitle = "Chỉnh sửa sự kiện";
            }

            if (Request.HttpMethod == "POST" && Request.Form["btnAction"] == "save")
            {
                SaveEvent();
                return;
            }

            if (!IsPostBack && EventId > 0)
            {
                LoadEventData();
            }
        }

        private void LoadEventData()
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = "SELECT * FROM Events WHERE Id = @id AND IsDeleted = 0";

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
                            CurrentTitle = r["Title"].ToString();
                            CurrentDescription = r["Description"].ToString();
                            CurrentStartTime = Convert.ToDateTime(r["StartTime"]).ToString("yyyy-MM-ddTHH:mm");
                            CurrentEndTime = Convert.ToDateTime(r["EndTime"]).ToString("yyyy-MM-ddTHH:mm");
                            CurrentLocation = r["Location"].ToString();
                            CurrentMaxCapacity = r["MaxCapacity"].ToString();
                            CurrentStatus = r["Status"].ToString();

                            if (r["RegistrationDeadline"] != DBNull.Value)
                                CurrentRegistrationDeadline = Convert.ToDateTime(r["RegistrationDeadline"]).ToString("yyyy-MM-ddTHH:mm");
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

        private void SaveEvent()
        {
            string title = Request.Form["txtTitle"]?.Trim();
            string description = Request.Form["txtDescription"] ?? "";
            string startStr = Request.Form["txtStartTime"];
            string endStr = Request.Form["txtEndTime"];
            string location = Request.Form["txtLocation"]?.Trim();
            string maxStr = Request.Form["txtMaxCapacity"];
            string deadlineStr = Request.Form["txtRegistrationDeadline"];
            string status = Request.Form["ddlStatus"] ?? "Draft";

            // Validation cơ bản
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(startStr) || string.IsNullOrEmpty(endStr) || string.IsNullOrEmpty(location) || string.IsNullOrEmpty(maxStr))
            {
                ShowAlert("Vui lòng điền đầy đủ các trường bắt buộc!");
                return;
            }

            DateTime startTime = DateTime.Parse(startStr);
            DateTime endTime = DateTime.Parse(endStr);
            int maxCapacity = int.Parse(maxStr);
            DateTime? deadline = string.IsNullOrEmpty(deadlineStr) ? (DateTime?)null : DateTime.Parse(deadlineStr);

            if (endTime <= startTime)
            {
                ShowAlert("Ngày kết thúc phải sau ngày bắt đầu!");
                return;
            }
            int createdBy = Convert.ToInt32(Session["UserId"]);

            // Upload ảnh nếu có
            string imageUrl = null;
            HttpPostedFile postedFile = Request.Files["eventBanner"];
            if (postedFile != null && postedFile.ContentLength > 0)
            {
                string ext = Path.GetExtension(postedFile.FileName).ToLower();
                if (new[] { ".jpg", ".jpeg", ".png", ".gif" }.Contains(ext))
                {
                    string fileName = Guid.NewGuid() + ext;
                    string savePath = Server.MapPath("~/Uploads/") + fileName;
                    postedFile.SaveAs(savePath);
                    imageUrl = fileName;
                }
                else
                {
                    ShowAlert("Chỉ chấp nhận file ảnh JPG, JPEG, PNG, GIF!");
                    return;
                }
            }

            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql;

            if (EventId == 0)
            {
                // Thêm mới – thêm cột CreatedBy
                sql = @"INSERT INTO Events 
                (Title, Description, StartTime, EndTime, Location, MaxCapacity, RegistrationDeadline, 
                 Status, ImageUrl, CurrentRegistrations, CreatedAt, CreatedBy)
                VALUES (@title, @desc, @start, @end, @loc, @max, @deadline, 
                        @status, @image, 0, NOW(), @createdBy)";
            }
            else
            {
                sql = @"UPDATE Events SET 
                Title = @title, Description = @desc, StartTime = @start, EndTime = @end,
                Location = @loc, MaxCapacity = @max, RegistrationDeadline = @deadline, Status = @status";
                if (imageUrl != null) sql += ", ImageUrl = @image";
                sql += " WHERE Id = @id";
            }

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@title", title);
                    cmd.Parameters.AddWithValue("@desc", description);
                    cmd.Parameters.AddWithValue("@start", startTime);
                    cmd.Parameters.AddWithValue("@end", endTime);
                    cmd.Parameters.AddWithValue("@loc", location);
                    cmd.Parameters.AddWithValue("@max", maxCapacity);
                    cmd.Parameters.AddWithValue("@deadline", deadline.HasValue ? (object)deadline.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@status", status);

                    // Xử lý ImageUrl
                    if (imageUrl != null)
                        cmd.Parameters.AddWithValue("@image", imageUrl);
                    else if (EventId == 0)
                        cmd.Parameters.AddWithValue("@image", DBNull.Value); 

                    // CreatedBy chỉ thêm khi INSERT
                    if (EventId == 0)
                        cmd.Parameters.AddWithValue("@createdBy", createdBy);

                    if (EventId > 0)
                        cmd.Parameters.AddWithValue("@id", EventId);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }

            Response.Redirect("EventManagement.aspx", false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private void ShowAlert(string message)
        {
            string script = $"alert('{message.Replace("'", "\\'")}');";
            ClientScript.RegisterStartupScript(this.GetType(), "alert", script, true);
        }
    }
}