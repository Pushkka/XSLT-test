using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Linq;
using Microsoft.Win32;
using net.sf.saxon.functions;
using Saxon.Api;

namespace XSLT_test
{
    public enum Month
    {
        january,
        february,
        march,
        april,
        may,
        june,
        july,
        august,
        september,
        october,
        november,
        december
    }

    public partial class MainWindow : Window
    {
        private ObservableCollection<EmployeeGroup> _employeeGroups;

        public MainWindow()
        {
            InitializeComponent();
            _employeeGroups = new ObservableCollection<EmployeeGroup>();
            EmployeesTreeView.ItemsSource = _employeeGroups;
        }

        private void FileSelector_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "XML файлы (*.xml)|*.xml|Все файлы (*.*)|*.*",
                Title = "Выберите XML файл для обработки",
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string inputXml = openFileDialog.FileName;
                LoadPayItems(inputXml);
            }
        }

        private void LoadPayItems(string filePath)
        {
            _employeeGroups.Clear();
            XDocument doc = XDocument.Load(filePath);

            var items = doc.Descendants("item");
            var grouped = items.GroupBy(i => new
            {
                Name = i.Attribute("name")?.Value ?? "",
                Surname = i.Attribute("surname")?.Value ?? ""
            });

            foreach (var group in grouped)
            {
                var employeeGroup = new EmployeeGroup
                {
                    Name = group.Key.Name,
                    Surname = group.Key.Surname
                };

                foreach (var item in group)
                {
                    double.TryParse(item.Attribute("amount")?.Value.Replace(".",","), out double amount);
                    Enum.TryParse<Month>(item.Attribute("mount")?.Value, true, out Month month);
                    employeeGroup.Salaries.Add(new SalaryRecord
                    {
                        Amount = amount,
                        Month = month
                    });
                }

                _employeeGroups.Add(employeeGroup);
            }
        }

        private void RunXsltTransformation(string inputXml, string xsltPath, string outputXml)
        {
            Processor processor = new Processor();
            XsltCompiler compiler = processor.NewXsltCompiler();
            XsltExecutable executable = compiler.Compile(new Uri(xsltPath));
            XsltTransformer transformer = executable.Load();

            DocumentBuilder builder = processor.NewDocumentBuilder();
            builder.BaseUri = new Uri(inputXml);
            XdmNode inputDoc = builder.Build(new Uri(inputXml));

            transformer.InitialContextNode = inputDoc;

            Serializer serializer = processor.NewSerializer();
            serializer.SetOutputFile(outputXml);
            transformer.Run(serializer);
        }

        private void AddTotalSalaryToEmployees(string filePath)
        {
            XDocument doc = XDocument.Load(filePath);
            foreach (var employee in doc.Descendants("Employee"))
            {
                double sum = employee.Elements("salary")
                    .Sum(s => double.Parse(s.Attribute("amount")?.Value.Replace(',', '.') ?? "0", CultureInfo.InvariantCulture));
                
                employee.SetAttributeValue("totalSalary", sum.ToString("F2", CultureInfo.InvariantCulture));
            }
            doc.Save(filePath);
        }

        private void AddTotalAmountToPay(string filePath)
        {
            XDocument doc = XDocument.Load(filePath);
            var payElement = doc.Element("Pay");
            if (payElement != null)
            {
                double totalSum = 0;
                foreach (var item in payElement.Descendants("item"))
                {
                    if (item.Attribute("amount") != null)
                    {
                        totalSum += double.Parse(item.Attribute("amount").Value.Replace(',', '.'), CultureInfo.InvariantCulture);
                    }
                }
                payElement.SetAttributeValue("totalAmount", totalSum.ToString("F2", CultureInfo.InvariantCulture));
                doc.Save(filePath);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_employeeGroups.Count == 0) return;
        }
    }

    public class EmployeeGroup : INotifyPropertyChanged
    {
        private string _name;
        private string _surname;

        public EmployeeGroup()
        {
            Salaries = new ObservableCollection<SalaryRecord>();
            Salaries.CollectionChanged += Salaries_CollectionChanged;
        }

        private void Salaries_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (SalaryRecord item in e.OldItems)
                    item.PropertyChanged -= SalaryRecord_PropertyChanged;
            }
            if (e.NewItems != null)
            {
                foreach (SalaryRecord item in e.NewItems)
                    item.PropertyChanged += SalaryRecord_PropertyChanged;
            }
            OnPropertyChanged(nameof(TotalSalary));
        }

        private void SalaryRecord_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SalaryRecord.Amount))
            {
                OnPropertyChanged(nameof(TotalSalary));
            }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public string Surname
        {
            get => _surname;
            set { _surname = value; OnPropertyChanged(nameof(Surname)); }
        }

        public string TotalSalary => Salaries.Sum(s => s.Amount).ToString("F2");

        public ObservableCollection<SalaryRecord> Salaries { get; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SalaryRecord : INotifyPropertyChanged
    {
        private double _amount;
        private Month _month;

        public double Amount
        {
            get => _amount;
            set { _amount = value; OnPropertyChanged(nameof(Amount)); }
        }

        public Month Month
        {
            get => _month;
            set { _month = value; OnPropertyChanged(nameof(Month)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
