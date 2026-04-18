using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using Microsoft.Win32;
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

    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        #region Private

        private string _openedFile { get; set; }
        private ObservableCollection<EmployeeGroup> _employeeGroups;

        #endregion

        #region Public

        public string OpenedFile
        {
            get => _openedFile;
            set { _openedFile = value; OnPropertyChanged(nameof(OpenedFile)); }
        }
        public ICommand AddEmployeeCommand { get; }
        public ICommand AddSalaryCommand { get; }
        public ICommand DeleteEmployeeCommand { get; }
        public ICommand DeleteSalaryCommand { get; }
        public ICommand SaveCommand { get; }

        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();
            _employeeGroups = new ObservableCollection<EmployeeGroup>();
            EmployeesTreeView.ItemsSource = _employeeGroups;

            AddEmployeeCommand = new RelayCommand(AddEmployee);
            AddSalaryCommand = new RelayCommand(AddSalary);
            DeleteEmployeeCommand = new RelayCommand(DeleteEmployee);
            DeleteSalaryCommand = new RelayCommand(DeleteSalary);
            SaveCommand = new RelayCommand(SaveExecute);

            DataContext = this;
        }

        #endregion

        #region Private Methods

        //Выбор файла.
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
                OpenedFile = openFileDialog.FileName;
                LoadPayItems(OpenedFile);
            }
        }


        //Загрузка текущих элементов.
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
                    double.TryParse(item.Attribute("amount")?.Value.Replace(".", ","), out double amount);
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



        private void AddEmployee(object parameter)
        {
            _employeeGroups.Add(new EmployeeGroup
            {
                Name = "Новый",
                Surname = "Сотрудник"
            });
        }

        private void DeleteEmployee(object parameter)
        {
            if (parameter is EmployeeGroup group)
            {
                _employeeGroups.Remove(group);
            }
        }

        private void AddSalary(object parameter)
        {
            if (parameter is EmployeeGroup group)
            {
                group.Salaries.Add(new SalaryRecord { Amount = 0, Month = Month.january });
            }
        }

        private void DeleteSalary(object parameter)
        {
            if (parameter is SalaryRecord record)
            {
                var group = _employeeGroups.FirstOrDefault(g => g.Salaries.Contains(record));
                group?.Salaries.Remove(record);
            }
        }

        private void SaveExecute(object parameter)
        {
            if (_employeeGroups.Count == 0 || string.IsNullOrEmpty(OpenedFile)) return;

            try
            {
                SaveCurrentDataToFile(OpenedFile);
                
                string xsltPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Transform.xslt");
                string employeesXml = Path.Combine(Path.GetDirectoryName(OpenedFile), "Employees.xml");

                RunXsltTransformation(OpenedFile, xsltPath, employeesXml);
                AddTotalSalaryToEmployees(employeesXml);
                AddTotalAmountToPay(OpenedFile);

                MessageBox.Show($"Данные сохранены и преобразованы!\nФайл: {employeesXml}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        //Пересохраняет файл.
        private void SaveCurrentDataToFile(string filePath)
        {
            Console.WriteLine("SaveCurrentDataToFile");

            XDocument doc = XDocument.Load(filePath);
            var payElement = doc.Element("Pay");

            if (payElement != null)
            {
                var existingItems = payElement.Descendants("item").ToList();
                foreach (var item in existingItems)
                {
                    item.Remove();
                }

                foreach (var group in _employeeGroups)
                {
                    foreach (var salary in group.Salaries)
                    {
                        payElement.Add(new XElement("item",
                            new XAttribute("name", group.Name),
                            new XAttribute("surname", group.Surname),
                            new XAttribute("mount", salary.Month),
                            new XAttribute("amount", salary.Amount.ToString("F2"))
                        ));
                    }
                }

                doc.Save(filePath);
            }
        }

        //Запускает преобразование.
        private void RunXsltTransformation(string inputXml, string xsltPath, string outputXml)
        {
            Console.WriteLine("RunXsltTransformation");
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

        //Дописывает в элемент Employee атрибут, который отражает сумму всех amount/@salary этого элемента
        private void AddTotalSalaryToEmployees(string filePath)
        {
            Console.WriteLine("AddTotalSalaryToEmployees");
            XDocument doc = XDocument.Load(filePath);
            foreach (var employee in doc.Descendants("Employee"))
            {
                double sum = employee.Elements("salary")
                    .Sum(s => double.Parse(s.Attribute("amount")?.Value ?? "0"));
                
                employee.SetAttributeValue("totalSalary", sum.ToString("F2"));
            }
            doc.Save(filePath);
        }

        //Дописывает атрибут, который отражает сумму всех amount.
        private void AddTotalAmountToPay(string filePath)
        {
            Console.WriteLine("AddTotalAmountToPay");
            XDocument doc = XDocument.Load(filePath);
            var payElement = doc.Element("Pay");
            if (payElement != null)
            {
                double totalSum = 0;
                foreach (var item in payElement.Descendants("item"))
                {
                    if (item.Attribute("amount") != null)
                    {
                        totalSum += double.Parse(item.Attribute("amount").Value);
                    }
                }
                payElement.SetAttributeValue("totalAmount", totalSum.ToString("F2"));
                doc.Save(filePath);
            }
        }

        #endregion

        #region INotify

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
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
