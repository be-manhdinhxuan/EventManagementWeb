using System;
using System.Web.UI;

public partial class SiteMaster : MasterPage
{
    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            // Kiểm tra đã đăng nhập chưa
            if (Session["UserId"] != null)
            {
                //lblUserName.Text = Session["FullName"]?.ToString() ?? "User";
            }
            else
            {
                // Chưa đăng nhập → chuyển về trang Login
                Response.Redirect("~/Account/Login.aspx");
            }
        }
    }

    protected void lnkLogout_Click(object sender, EventArgs e)
    {
        Session.Clear();
        Session.Abandon();
        Response.Redirect("~/Account/Login.aspx");
    }
}