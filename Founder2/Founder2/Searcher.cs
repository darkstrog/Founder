using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Founder2
{
    public class Searcher
    {
        public delegate void SearchEventHandler(object sender, SearchEventArgs e);
        public delegate void CompleteEventHandler(object sender, CompleteEventArgs e);
        public delegate void ErrorEventHandler(object sender, ErrorEventArgs e);
        public delegate void ProcessedEventHandler(object sender, ProcessedUpEventArgs e);
        /// <summary>
        /// Информирует о ходе поиска, в какой папке или файле идет поиск
        /// </summary>
        public event SearchEventHandler SearchingNotify;
        /// <summary>
        /// Срабатывает когда находит файл с задаными критериями поиска
        /// </summary>
        public event SearchEventHandler FoundFile;
        /// <summary>
        /// Срабатывает при ошибках доступа к папкам и файлам
        /// </summary>
        public event ErrorEventHandler Error;
        /// <summary>
        /// Срабатывает при увеличении счетчика найденых
        /// </summary>
        public event ProcessedEventHandler CountUp;
        /// <summary>
        /// Срабатывает при увеличении счетчика обработанных файлов
        /// </summary>
        public event ProcessedEventHandler ProcessedUp;
        /// <summary>
        /// Срабатывает при запуске поиска
        /// </summary>
        public event EventHandler StartSearching;
        /// <summary>
        /// Срабатывает при завершении поиска
        /// </summary>
        public event CompleteEventHandler CompleteSearch;
        public event CompleteEventHandler CancelSearch;
        public ManualResetEvent Busy;
        public CancellationToken CancelToken;
        ///найдено по критерию
        private int count;
        public int Count { get { return count; } private set { count = value; CountUp?.Invoke(this, new ProcessedUpEventArgs(count,processedDirs)); } }
        ///Обработанные файлы
        private int processed;
        public int Processed { get { return processed; } private set { processed = value; ProcessedUp?.Invoke(this, new ProcessedUpEventArgs(processed,processedDirs)); } }
        private int processedDirs;
        public int ProcessedDirs { get { return processedDirs; } private set { processedDirs = value; ProcessedUp?.Invoke(this, new ProcessedUpEventArgs(processed,processedDirs)); } }
        public List<string> ErrorPaths { get; private set; }
        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }
        ///Путь поиска
        public string SearchPath { get;}
        ///Шаблон поиска
        public string SearchPattern { get;}
        ///Искомый текст
        public string SearchText { get;}
        public string CurrentSearchPath { get; private set; }
        private StreamReader sr { get; set; }

        public Searcher(string _searchPath, string _searchPattern, string _searchText, ManualResetEvent _busy, CancellationToken _cancelToken)
        {
            SearchPath = _searchPath;
            SearchPattern = _searchPattern;
            SearchText = _searchText;
            Busy = _busy;
            CancelToken = _cancelToken;
            ErrorPaths = new List<string>();
        }

        /// <summary>
        /// Возвращает перечисляемую коллекцию имен файлов которые соответствуют шаблону в указанном каталоге, с дополнительным просмотром вложенных каталогов
        /// </summary>
        /// <param name="path">Полный или относительный путь каталога в котором выполняется поиск</param>
        /// <param name="searchPattern">Шаблон поиска файлов</param>
        /// <param name="searchOption">Одно из значений перечисления SearchOption указывающее нужно ли выполнять поиск во вложенных каталогах или только в указанном каталоге</param>
        /// <param name="word">Подстрока которую необходимо найти</param>
        /// <returns>Возвращает перечисляемую коллекцию полных имен файлов</returns>
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*.*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            var dirs = new Stack<string>();
            dirs.Push(path);

            while (dirs.Count > 0)
            {
                this.Busy.WaitOne();//Приостановка
                if (CancelToken.IsCancellationRequested == true)
                {
                    EndTime = DateTime.Now;
                    CancelSearch?.Invoke(this, new CompleteEventArgs("Поиск отменен!", Count, Processed, ErrorPaths, StartTime, EndTime, SearchPath, SearchPattern, SearchText));
                    yield break; 
                }//Отмена операции
                string currentDirPath = dirs.Pop();
                SearchingNotify?.Invoke(this, new SearchEventArgs($"Поиск в директории {currentDirPath}", currentDirPath));
                
                if (searchOption == SearchOption.AllDirectories)
                {
                    try
                    {
                        string[] subDirs = Directory.GetDirectories(currentDirPath);
                        foreach (string subDirPath in subDirs)
                        {
                            this.Busy.WaitOne();//Приостановка
                            if (CancelToken.IsCancellationRequested == true)
                            {
                                EndTime = DateTime.Now;
                                CancelSearch?.Invoke(this, new CompleteEventArgs("Поиск отменен!", Count, Processed, ErrorPaths, StartTime, EndTime, SearchPath, SearchPattern, SearchText));
                                yield break;
                            }//Отмена операции
                            dirs.Push(subDirPath);
                        }
                    }
                    catch (UnauthorizedAccessException e)
                    {
                        this.Error?.Invoke(this, new ErrorEventArgs(e.Message));
                        ErrorPaths.Add(currentDirPath);
                        continue;
                    }
                    catch (DirectoryNotFoundException e)
                    {
                        this.Error?.Invoke(this, new ErrorEventArgs(e.Message));
                        ErrorPaths.Add(currentDirPath);
                        continue;
                    }
                }

                string[] files = null;
                try
                {
                    CurrentSearchPath = currentDirPath;
                    files = Directory.GetFiles(currentDirPath, searchPattern);
                    ProcessedDirs++;
                }
                catch (UnauthorizedAccessException e)
                {
                    this.Error?.Invoke(this, new ErrorEventArgs(e.Message));
                    ErrorPaths.Add(currentDirPath);
                    continue;
                }
                catch (DirectoryNotFoundException e)
                {
                    this.Error?.Invoke(this, new ErrorEventArgs(e.Message));
                    ErrorPaths.Add(currentDirPath);
                    continue;
                }

                foreach (string filePath in files)
                {
                    yield return filePath;
                }
            }
        }
        /// <summary>
        /// Осуществляет поиск поиск текста в файле поддерживает регулярные выражения
        /// </summary>
        /// <param name="_path">Полное имя файла</param>
        /// <param name="_text"></param>
        public async Task searchInFile(string _path,string _text)
        {
        SearchingNotify?.Invoke(this, new SearchEventArgs($"Поиск текста: \"{_text}\" в файле {_path}",_path));
        try
        {
            using (sr = new StreamReader(_path))
            {
                string data = await sr.ReadToEndAsync();
                Regex regex = new Regex(_text);
                MatchCollection matches = regex.Matches(data);
                if (matches.Count > 0)
                {
                    this.Count++;
                    FoundFile?.Invoke(this, new SearchEventArgs($"В файле {_path} найден текст{_text}", _path));
                }
            }
        }
        catch(Exception e) { this.Error?.Invoke(this, new ErrorEventArgs(e.Message)); ErrorPaths.Add(_path); }
        }

        /// <summary>
        /// Запускает поиск
        /// </summary>
        /// <param name="_searchText"></param>
        /// <returns></returns>
        public async Task StartSearch()
        {
            ///Время начала поиска
            StartTime = DateTime.Now;
            var isFound = EnumerateFiles(SearchPath, SearchPattern, SearchOption.AllDirectories);
            if (!String.IsNullOrEmpty(SearchText)||!String.IsNullOrWhiteSpace(SearchText))
            {
                foreach (var file in isFound)
                {
                    this.Busy.WaitOne();//Приостановка
                    if (CancelToken.IsCancellationRequested == true)
                    {
                        EndTime = DateTime.Now;
                        CancelSearch?.Invoke(this, new CompleteEventArgs("Поиск отменен!", Count, Processed, ErrorPaths, StartTime, EndTime, SearchPath, SearchPattern, SearchText));
                        return;
                    }//Отмена операции
                    await searchInFile(file, SearchText);
                    Processed++;
                }
            }
            else
            {
                foreach (var file in isFound)
                {
                    this.Busy.WaitOne();//Приостановка
                    if (CancelToken.IsCancellationRequested == true)
                    {
                        EndTime = DateTime.Now;
                        CancelSearch?.Invoke(this, new CompleteEventArgs("Поиск отменен!", Count, Processed, ErrorPaths, StartTime, EndTime, SearchPath, SearchPattern, SearchText));
                        return;
                    }//Отмена операции
                    Count++;
                    FoundFile?.Invoke(this, new SearchEventArgs($"Найден файл соответствующий шаблону {file}", file));
                }
            }
            EndTime = DateTime.Now;
            CompleteSearch?.Invoke(this, new CompleteEventArgs($"Поиск завершен", Count, Processed, ErrorPaths,StartTime,EndTime, SearchPath,SearchPattern,SearchText));
        }

        public class StartSearchingEventArgs : EventArgs
        {
            public string Message { get; }
            public string Path { get; }
            public DateTime StartTime { get; }
            public string SearchPattern { get; }
            public string SearchSubString { get; }
            public StartSearchingEventArgs(string _message, string _path, string _searchPattern, string _subString)
            {
                this.Message= _message;
                this.Path=_path;
                this.StartTime=DateTime.Now;
                this.SearchPattern=_searchPattern;
                this.SearchSubString=_subString;
            }
        }
        public class ErrorEventArgs : EventArgs
        { 
            public string Message { get; set; }
            public ErrorEventArgs(string _message)
            {
                this.Message = _message;
            }
        }
        public class SearchEventArgs : EventArgs
        {
            public string FilePath { get; }
            public string Message { get; }

            public SearchEventArgs(string mes, string filepath)
            {
                Message = mes;
                FilePath = filepath;
            }
        }
        public class ProcessedUpEventArgs:EventArgs
        {
            public int Count { get; }
            public int DirsCount { get; }
            public ProcessedUpEventArgs(int _count,int _dirsCount)
            {
                Count = _count;
                DirsCount = _dirsCount;
            }
        }
        public class CompleteEventArgs:EventArgs
        {
            public string SearchPath { get;}
            public string SearchPattern { get; }
            public string SearchText { get; }
            public int Count { get;}
            public int Processed { get;}
            public string[] ErrorsPaths { get; }
            public string Message { get; }
            public DateTime StartTime { get; }
            public DateTime EndTime { get; }
            public TimeSpan Elaps { get; }
            public CompleteEventArgs(string _message, int _count, int _processed, List<string> _errorsPaths,DateTime _startTime,DateTime _endTime, string _searchPath, string _searchPattern="*.*", string _searchText="")
            {
                this.SearchPath = _searchPath;
                this.SearchPattern = _searchPattern;
                this.SearchText = _searchText;
                this.Count = _count;
                this.Processed = _processed;
                this.Message = _message;
                this.ErrorsPaths = _errorsPaths?.ToArray();
                this.StartTime = _startTime;
                this.EndTime = _endTime;
                this.Elaps = _endTime - _startTime;
            }
        }
       
    }
}
