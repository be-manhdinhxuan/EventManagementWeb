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
        private const string ToastSessionKey = "ToastMessage";
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                CheckAndShowToast();
                LoadUpcomingEvents();
                LoadFeaturedEvent();
            }
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
                    WHERE e.Status = 'Draft' 
                      AND e.IsDeleted = 0 
                      AND e.StartTime > NOW()
                    ORDER BY e.StartTime ASC
                    LIMIT 6";

                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    conn.Open();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);

                        if (dt.Rows.Count > 0)
                        {
                            rptEvents.DataSource = dt;
                            rptEvents.DataBind();
                            pnlNoEvents.Visible = false;
                        }
                        else
                        {
                            pnlNoEvents.Visible = true;
                        }
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
                    SELECT Title, Description, ImageUrl 
                    FROM Events 
                    WHERE Status = 'Published' AND IsDeleted = 0 AND StartTime > NOW()
                    ORDER BY CurrentRegistrations DESC, StartTime ASC 
                    LIMIT 1";

                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    conn.Open();
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            
                        }
                    }
                }
            }
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