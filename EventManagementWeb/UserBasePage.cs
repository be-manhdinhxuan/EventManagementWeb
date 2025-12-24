using System;

public class UserBasePage : System.Web.UI.Page
{
    protected override void OnInit(EventArgs e)
    {
        base.OnInit(e);

        if (Session["UserId"] == null)
        {
            Response.Redirect("~/Account/Login.aspx");
        }
        // Nếu là Admin thì bắt quay về Dashboard Admin, không cho xem trang User
        else if (Session["Role"]?.ToString() == "Admin")
        {
            Response.Redirect("~/Admin/Dashboard.aspx");
        }
    }
}