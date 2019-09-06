using Common;
using DataAccess;
using System;
using System.Data;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Timers;

namespace ReadAMP
{
    public partial class ReadAMPService : ServiceBase
    {
        private static System.Timers.Timer time = new System.Timers.Timer();
        public ReadAMPService()
        {
            InitializeComponent();

        }

        protected override void OnStart(string[] args)
        {
            if (Utilities.getConnection())
            {
                WriteToFile("Connect Database succsessfull - " + DateTime.Now.ToString());
            }
            else
            {
                WriteToFile("Error connect database! ");
            }
            WriteToFile("In OnStart --- Read AMP - " + DateTime.Now.ToString());


            time.Elapsed += new System.Timers.ElapsedEventHandler(time_elapsed);

            //  time.Interval = 300000; 5 phút.5*600000
            //time.Interval = 180000; //3 phút
            //time.Interval = 60000; //1 phút
            time.Interval = Utilities.acAppConfig.ITime;
            time.Start();
        }

        private void time_elapsed(object sender, ElapsedEventArgs e)
        {

            WriteToFile("Reading data base - " + DateTime.Now.ToString());
            time.Enabled = false;
            time.Stop();
            DataTable dt = redDataFromATM();
            string sdate = DateTime.Now.ToString("yyyy-MM-dd") + " 00:00:00.000";
            string postData = "mutation upsert_chamcong_time_in_outs{ insert_chamcong_time_in_outs(objects: [";
            //WriteToFile("Tong ban ghi:"+dt.Rows.Count.ToString());
            try
            {
                if (dt.Rows.Count > 0)
                {
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        if (i == 0)
                        {

                            postData = postData + "  { ma_nv:'" + dt.Rows[i][0].ToString() + "', ma_id:" + dt.Rows[i][1].ToString() + "}";
                            //                     postData = postData + "  { ma_nv:\\\"" + dt.Rows[i][0].ToString() + "\\\", ma_id:" + dt.Rows[i][1].ToString() + ", day_work:\\\"" + (dt.Rows[i][2].ToString().Count() == 0 ? sdate : dt.Rows[i][2].ToString()) + "\\\",timein0 :\\\"" + (dt.Rows[i][3].ToString().Count() == 0 ? sdate : dt.Rows[i][3].ToString()) + "\\\",timeout0 :\\\"" + (dt.Rows[i][4].ToString().Count() == 0 ? sdate : dt.Rows[i][4].ToString()) + "\\\"}";

                        }
                        else
                        {

                            postData = postData + ",  { ma_nv:'" + dt.Rows[i][0].ToString() + "', ma_id:" + dt.Rows[i][1].ToString() + "}";
                            //postData = postData + ",  { ma_nv:\\\"" + dt.Rows[i][0].ToString() + "\\\", ma_id:" + dt.Rows[i][1].ToString() + ", day_work:\\\"" + (dt.Rows[i][2].ToString().Count() == 0 ? sdate : dt.Rows[i][2].ToString()) + "\\\",timein0 :\\\"" + (dt.Rows[i][3].ToString().Count() == 0 ? sdate : dt.Rows[i][3].ToString()) + "\\\",timeout0 :\\\"" + (dt.Rows[i][4].ToString().Count() == 0 ? sdate : dt.Rows[i][4].ToString()) + "\\\"}";

                        }

                    }
                    postData = postData + " ], on_conflict: {constraint:time_in_outs_ma_nv_ma_id_day_work_key update_columns: [timein0,timeout0]}) {returning {ma_id}}}\",\"variables\":null,\"operationName\":\"upsert_chamcong_time_in_outs\"}";

                    postData = "\"mutation delete { delete_chamcong_check_in_out(   where: { } ) {  affected_rows} } \"";
                    postToServer(postData);
                    WriteToFile(postData);

                }
            }
            catch (Exception ex)
            {
                WriteToFile("loi:" + ex.Message);

            }

            time.Enabled = true;
            time.Start();
        }



        private void postToServer(string postData)
        {
            try
            {

                // Create a request using a URL that can receive a post.   
                WebRequest request = (HttpWebRequest)WebRequest.Create("https://qlth.hpz.vn/v1/graphql");
                // Set the Method property of the request to POST.  
                request.Method = "POST";
                //request.Headers.Add("Authorization","hpz");
                request.Credentials = new NetworkCredential("x-hasura-admin-secret", "hpz");
                byte[] byteArray = Encoding.UTF8.GetBytes(postData);
                                // Set the ContentType property of the WebRequest.  
                //request.ContentType = "application/x-www-form-urlencoded";
                request.ContentType = "application/json; charset=utf-8";
                // Set the ContentLength property of the WebRequest.  
                request.ContentLength = byteArray.Length;

                // Get the request stream.  
                Stream dataStream = request.GetRequestStream();
                // Write the data to the request stream.  
                dataStream.Write(byteArray, 0, byteArray.Length);
                // Close the Stream object.  
                dataStream.Close();

                // Get the response.  
                WebResponse response = request.GetResponse();
                // Display the status.  
                //Console.WriteLine(((HttpWebResponse)response).StatusDescription);
                WriteToFile(((HttpWebResponse)response).StatusDescription);
                // Get the stream containing content returned by the server.  
                // The using block ensures the stream is automatically closed.
                using (dataStream = response.GetResponseStream())
                {
                    // Open the stream using a StreamReader for easy access.  
                    StreamReader reader = new StreamReader(dataStream);
                    // Read the content.  
                    string responseFromServer = reader.ReadToEnd();
                    // Display the content.  
                    WriteToFile(responseFromServer);
                }

                // Close the response.  
                response.Close();


            }
            catch (Exception ee)
            {
                WriteToFile(ee.Message);
            }
        }

        private DataTable redDataFromATM()
        {
            DataTable dt = new DataTable();
            try
            {
                DbAccess db = new DbAccess();
                //string sql = string.Format("SELECT * From vwTimeInOut");
                db.CreateNewSqlCommand();
                db.AddParameter("@DayWork", DateTime.Now.ToString("yyyy/MM/dd"));
                //db.AddParameter("@DayWork", "2017/09/06");
                dt = db.ExecuteDataTable("sp_ViewInOut");
                WriteToFile("Reading data succsessfull - " + DateTime.Now.ToString());

            }

            catch (Exception ee)
            {
                WriteToFile(ee.Message);

            }
            return dt;
        }

        protected override void OnStop()
        {
            WriteToFile("In OnStop --- Read AMP - " + DateTime.Now.ToString());
        }
        public void WriteToFile(string Message)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
            if (!System.IO.Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            string filepath = AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\ServiceLog_" + DateTime.Now.Date.ToShortDateString().Replace('/', '_') + ".txt";
            if (!File.Exists(filepath))
            {
                // Create a file to write to.   
                using (StreamWriter sw = File.CreateText(filepath))
                {
                    sw.WriteLine(Message);
                }
            }
            else
            {
                using (StreamWriter sw = File.AppendText(filepath))
                {
                    sw.WriteLine(Message);
                }
            }
        }
    }
}