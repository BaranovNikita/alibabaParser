using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using AlibabaParser.models;
using HtmlAgilityPack;
using Excel = Microsoft.Office.Interop.Excel;
namespace AlibabaParser
{
    public partial class Form1 : Form
    {
        private string currentUrl;
        private string currentCompany;
        public Form1()
        {
            InitializeComponent();
            if (Properties.Settings.Default.items != null)
            {
                foreach (var item in AlibabaParser.Properties.Settings.Default.items.Split(','))
                {
                    if (!string.IsNullOrEmpty(item))
                        urlList.Items.Add(item);
                }
            }

            setBackgroundWorker();
        }

        private void setBackgroundWorker()
        {
            backgroundWorker1.ProgressChanged += (sender, args) =>
            {
                Console.WriteLine("args.ProgressPercentage: " + args.ProgressPercentage);
                progressBar1.Value = args.ProgressPercentage;
                if (args.ProgressPercentage == 0)
                {
                    progressLabel.Text = "Load page html: " + currentUrl;
                }
                else
                {
                    progressLabel.Text = currentCompany;
                }

            };
            backgroundWorker1.DoWork += (sender, args) =>
            {
                Console.WriteLine("DOWORK");
                HtmlAgilityPack.HtmlDocument document = null;
                var companyList = new List<Company>();
                foreach (ListViewItem item in getListViewItems(urlList))
                {
                    try
                    {
                        document = getHtmlFromUrl(item.Text);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        MessageBox.Show("alibaba not response", "ERROR");
                    }
                    var currentPageCompany = CreateCompanyList(document, item.Text);
                    companyList.AddRange(currentPageCompany);
                }
                args.Result = companyList;
            };
            backgroundWorker1.RunWorkerCompleted += (sender, args) =>
            {
                Console.WriteLine("COMPLETE");
                var result = args.Result as List<Company>;
                progressBar1.Value = 0;
                progressLabel.Text = "Creating excel file";
                DisplayInExcel(result);
            };
        }

        private IEnumerable<Company> CreateCompanyList(HtmlAgilityPack.HtmlDocument document, string url)
        {
            var nodes = document.DocumentNode.QuerySelectorAll(".item-main");
            var companyList = new List<Company>();
            foreach (var node in nodes)
            {
                currentCompany = node.QuerySelector(".item-title h2.title").InnerText.Trim('\n').Trim();
                var percent = Convert.ToInt32(Math.Round((double)companyList.Count / nodes.Count * 100));
                backgroundWorker1.ReportProgress(percent > 100 ? 100 : percent);
                var company = new Company
                {
                    Name = node.QuerySelector(".item-title h2.title").InnerText.Trim('\n').Trim(),
                    Url = node.QuerySelector(".item-title h2.title a").Attributes["href"].Value,
                    YrGold =
                        int.Parse(
                            node.QuerySelector(".item-title .ico-year span").Attributes["class"].Value.Replace("gs", ""))
                };
                var transitionLevels = node.QuerySelectorAll(".content .s-val span");
                company.TransitionLevel = getTransitionLevel(transitionLevels);
                try
                {
                    company.AmountTransaction = int.Parse(node.QuerySelector(".content .lab b").InnerText);
                }
                catch (NullReferenceException)
                {
                    company.AmountTransaction = 0;
                }
                try
                {
                    var income =
                        node.QuerySelector(".content .num .ui2-icon-dollar").ParentNode.InnerText.Replace("$", "").Replace(",", "");
                    if (income.EndsWith("+"))
                    {
                        company.Income = int.Parse(income.Replace("+", ""));
                    }
                    else
                    {
                        company.Income = -int.Parse(income.Replace("-", ""));
                    }
                }
                catch (NullReferenceException)
                {
                    company.Income = 0;
                }
                try
                {
                    company.ResponceRate = double.Parse(node.QuerySelector(".content .num a").InnerText.Replace("%", "").Replace(".", ","));
                }
                catch (NullReferenceException)
                {
                    company.ResponceRate = 0;
                }
                company.Country = node.QuerySelector(".content .flag+span").InnerText;
                try
                {
                    company.TotalRevenue = node.QuerySelector(".content [data-reve]").InnerText;
                }
                catch (NullReferenceException)
                {
                    company.TotalRevenue = "0";
                }
                companyList.Add(company);
            }
            return companyList;
        }

        private HtmlAgilityPack.HtmlDocument getHtmlFromUrl(string url)
        {
            backgroundWorker1.ReportProgress(0);
            currentUrl = url;
            var stringHtml = new System.Net.WebClient().DownloadString(url);
            var html = new HtmlAgilityPack.HtmlDocument();
            html.LoadHtml(stringHtml);
            return html;
        }

        private double getTransitionLevel(IEnumerable<HtmlNode> nodes)
        {
            double diamonds = 0;
            foreach (var level in nodes)
            {
                if (level.Attributes["class"].Value.Contains("diamond-level-one"))
                {
                    diamonds++;
                }
                if (level.Attributes["class"].Value.Contains("diamond-level-half"))
                {
                    diamonds += 0.5;
                }
            }
            return diamonds;
        }

        private void addUrlBtn_Click(object sender, EventArgs e)
        {
            var item = urlList.Items.Add(string.Empty);
            item.BeginEdit();
        }

        private void urlList_DoubleClick(object sender, EventArgs e)
        {
            if (urlList.SelectedItems.Count > 0)
                urlList.SelectedItems[0].BeginEdit();
        }

        private void urlList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                urlList.SelectedItems[0].Remove();
            }
        }

        private void urlList_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (urlList.FocusedItem.Bounds.Contains(e.Location))
                {
                    contextMenuStrip1.Show(Cursor.Position);
                }
            }
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (urlList.SelectedItems.Count > 0)
                urlList.SelectedItems[0].Remove();
        }

        private void changeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (urlList.SelectedItems.Count > 0)
                urlList.SelectedItems[0].BeginEdit();
        }

        private void parseList_Click(object sender, EventArgs e)
        {

            backgroundWorker1.RunWorkerAsync();

            // backgroundWorker1.CancelAsync();
        }
        private delegate ListView.ListViewItemCollection GetItems(ListView lstview);

        private ListView.ListViewItemCollection getListViewItems(ListView lstview)
        {
            var temp = new ListView.ListViewItemCollection(new ListView());
            if (lstview.InvokeRequired)
                return (ListView.ListViewItemCollection)Invoke(new GetItems(getListViewItems), lstview);
            foreach (ListViewItem item in lstview.Items)
                temp.Add((ListViewItem)item.Clone());
            return temp;
        }
        static void DisplayInExcel(IEnumerable<Company> companyList)
        {
            var excelApp = new Excel.Application();
            var workbook = excelApp.Workbooks.Add();
            Excel._Worksheet workSheet = (Excel.Worksheet)excelApp.ActiveSheet;
            object misValue = System.Reflection.Missing.Value;
            workSheet.Cells[1, "A"] = "Company name";
            workSheet.Cells[1, "B"] = "Company url";
            workSheet.Cells[1, "C"] = "Gold Yr";
            workSheet.Cells[1, "D"] = "Transaction level";
            workSheet.Cells[1, "E"] = "Amount transaction 6months";
            workSheet.Cells[1, "F"] = "$ ( 6 months)";
            workSheet.Cells[1, "G"] = "Response rate";
            workSheet.Cells[1, "H"] = "Country/region";
            workSheet.Cells[1, "I"] = "Total revenue";
            var row = 1;
            foreach (var company in companyList)
            {
                row++;
                workSheet.Cells[row, "A"] = company.Name;
                workSheet.Cells[row, "B"] = company.Url;
                workSheet.Cells[row, "C"] = company.YrGold;
                workSheet.Cells[row, "D"] = company.TransitionLevel;
                workSheet.Cells[row, "E"] = company.AmountTransaction;
                workSheet.Cells[row, "F"] = company.Income;
                workSheet.Cells[row, "G"] = company.ResponceRate;
                workSheet.Cells[row, "H"] = company.Country;
                workSheet.Cells[row, "I"] = company.TotalRevenue;
            }
            workSheet.Columns[1].AutoFit();
            workSheet.Columns[2].AutoFit();
            workSheet.Columns[3].AutoFit();
            workSheet.Columns[4].AutoFit();
            workSheet.Columns[5].AutoFit();
            workSheet.Columns[6].AutoFit();
            workSheet.Columns[7].AutoFit();
            workSheet.Columns[8].AutoFit();
            workSheet.Columns[9].AutoFit();
            var filePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\alibaba.xls";
            var saveFileDialog1 = new SaveFileDialog
            {
                Filter = "excel files (*.xls) | *.xls | All files(*.*) | *.*",
                RestoreDirectory = true
            };


            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                filePath = saveFileDialog1.FileName;
            }

            workbook.SaveAs(filePath);
            workbook.Close(true, misValue, misValue);
            excelApp.Quit();

            MessageBox.Show("Excel file created , you can find the file " + filePath);

        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            var i = 0;
            var array = new string[urlList.Items.Count];
            foreach (ListViewItem item in urlList.Items)
            {
                array[i] = urlList.Items[i].Text;
                i++;
            }
            AlibabaParser.Properties.Settings.Default.items = string.Join(",", array);
            AlibabaParser.Properties.Settings.Default.Save();
        }
    }
}
