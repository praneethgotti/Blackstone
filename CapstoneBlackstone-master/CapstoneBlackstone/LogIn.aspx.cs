using CapstoneBlackstone.C_SharpClasses;
using Connection;
using System;
using System.DirectoryServices;
using System.Drawing;

namespace CapstoneBlackstone
{
    public partial class LogIn : System.Web.UI.Page
    {
        /*Declaring page-wide global connection object for convenience*/
        DBConnect conn = new DBConnect();
        Validation valid = new Validation();
        protected void Page_Load(object sender, EventArgs e)
        {
            isAuthenticated();
        }

        protected void isAuthenticated()
        {
            if (Session["Authenticated"] != null)
            {
                Boolean isAuthenticated = Boolean.Parse(Session["Authenticated"].ToString());

                if (!isAuthenticated)
                {
                    /*Small easter egg*/
                    Response.Redirect("https://i1295.photobucket.com/albums/b632/iusethis123/heh_zps167096b6.gif");

                    /*Clearing Session to allow backward navigation after the easter egg*/
                    Session.Clear();
                }
                else if (isAuthenticated)
                {
                    lbl_Error.Text = "You are already logged-in, you may navigate back to the Home.aspx page.";
                    lbl_Error.ForeColor = Color.Green;
                }
            }
        }

        protected void btn_login_Click(object sender, EventArgs e)
        {

            /*1 - Grabbing the user's login info*/
            String Username = txb_TUID.Text;
            String Password = txb_Password.Text;

            /*2 - Resetting controls to default values*/
            lbl_Error.Text = "";

            /*3 - Basic validation*/
            if (Username.Equals(""))
            {
                lbl_Error.Text = "ERROR: The username field is empty.";
                lbl_Error.ForeColor = Color.Red;
            }
            else if (Password.Equals(""))
            {
                lbl_Error.Text = "ERROR: The password field is empty.";
                lbl_Error.ForeColor = Color.Red;
            }
            else if(valid.validateLogin(Username) || valid.validateLogin(Password))
            {
                lbl_Error.Text = "ERROR: Illegal input character was used.";
                lbl_Error.ForeColor = Color.Red;
            }
            else
            {
                /*3 - Checking that the Username and Password are both correct*/
                bool Correct_Login_Information = AuthenticateUser(Username, Password);
                if (!Correct_Login_Information)
                {
                    lbl_Error.Text = "ERROR: Your username or password is incorrect.";
                    lbl_Error.ForeColor = Color.Red;
                }
                else
                {
                    /*4 - Requesting Web Service information*/
                    TempleUser.LDAPuser Temple_Information = TempleUser.WebService.getLDAPEntryByAccessnet(Username);
                    TempleUser.StudentObj Student_Information = TempleUser.WebService.getStudentInfo(Temple_Information.templeEduID);

                    /*5 - Checking we received something from Web Services*/
                    if (Temple_Information == null)
                    {
                        lbl_Error.Text = "ERROR: Web Services did not return anything.";
                    }
                    else if (Temple_Information != null)
                    {
                        /*Populating the Session Object with the user's information*/
                        Session["TU_ID"] = Temple_Information.templeEduID;//TUID
                        Session["First_Name"] = Temple_Information.givenName;
                        Session["Last_Name"] = Temple_Information.sn;
                        Session["Email"] = Temple_Information.mail;
                        Session["Title"] = Temple_Information.title;
                        Session["Affiliation_Primary"] = Temple_Information.eduPersonPrimaryAffiliation;
                        Session["Affiliation_Secondary"] = Temple_Information.eduPersonAffiliation;

                        /*Security Session Variable*/
                        Session["Authenticated"] = true;
                        

                        /*If the user is also a student, we can also retreive their information and add them to the Session Object*/
                        if (Student_Information != null)
                        {
                            Session["School"] = Student_Information.school;
                            Session["Major_1"] = Student_Information.major1;
                            Session["Major_2"] = Student_Information.major2;
                        }

                        /*Successful Login - Allowed to be redirected to Home.aspx*/

                        DbMethods DbMethodsObj = new DbMethods();
                        bool test = DbMethodsObj.CheckIfAdminExists(Student_Information.tuid);
                        //check if user is an Admin
                        if (test == true)
                        {
                            //Security Session Variable for Admin
                            Session["AdminToken"] = true;
                            Response.Redirect("Admin.aspx");
                        }
                        else
                        {
                            //check if expert exists in system
                            int count = Convert.ToInt32(DbMethodsObj.CheckIfExpertExists(Student_Information.tuid));
                            if (count == 0)
                            {
                                Response.Redirect("CreateProfile.aspx");
                            }
                            else
                            {
                                SessionMethods sessionMethodsObj = new SessionMethods();
                                sessionMethodsObj.storeExpertDataInSession();
                                //change isActive to true
                                DbMethodsObj.SetExpertIsActiveTrue();
                                Expert expertProfileObj = (CapstoneBlackstone.Expert)Session["expertProfileObj"];
                                string user_name = expertProfileObj.username;

                                var x = Session["Authenticated"];
                                //redirect to expert page
                                Response.Redirect("ExpertPage.aspx?username=" + user_name);// conserve the session token at login
                            }
                        }
                    }
                }
            }
        }//end logIn button clickEvent

        /*Pre-written security Methods - DO NOT CHANGE*/
        public static bool AuthenticateUser(string strUID, string strPassword)
        {
            string strID = string.Empty;
            var entry = new DirectoryEntry();

            try
            {
                string strDN = getDNFromLDAP(strUID);
                entry.Path = "LDAP://rock.temple.edu/" + strDN;
                entry.Username = strDN;
                entry.Password = strPassword;
                entry.AuthenticationType = AuthenticationTypes.SecureSocketsLayer;
                strID = entry.Properties["sAMAccountName"][0].ToString();

            }
            catch
            {
                return false;
            }
            finally
            {
                entry.Close();
                entry.Dispose();
            }
            return true;
        }
        public static string getDNFromLDAP(string strUID)
        {
            var entry = new DirectoryEntry("LDAP://rock.temple.edu/ou=temple,dc=tu,dc=temple,dc=edu");
            entry.AuthenticationType = AuthenticationTypes.None;
            var mySearcher = new DirectorySearcher(entry);
            entry.Close();
            entry.Dispose();
            mySearcher.Filter = "(sAMAccountName=" + strUID + ")";
            var result = mySearcher.FindOne();
            mySearcher.Dispose();
            int nIndex = result.Path.LastIndexOf("/");
            string strDN = result.Path.Substring((nIndex + 1)).ToString().TrimEnd();
            return strDN;
        }
    }
}