using System;
using System.Web;

namespace EventManagementWeb
{
    public class AdminBasePage : System.Web.UI.Page
    {
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            // Kiểm tra: Nếu chưa đăng nhập HOẶC không phải Admin thì đẩy ra Login
            if (Session["UserId"] == null || Session["Role"]?.ToString() != "Admin")
            {
                Response.Redirect("~/Account/Login.aspx", false);
                Context.ApplicationInstance.CompleteRequest();
            }
        }
    }
}