using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Media;
using AutoUpdaterDotNET;
using Newtonsoft.Json.Linq;

using Color = System.Windows.Media.Color;
using Image = System.Windows.Controls.Image;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using Orientation = System.Windows.Controls.Orientation;
using MessageBox = System.Windows.MessageBox;

namespace BOLGAMES
{
    public partial class MainWindow : Window
    {
        private const string GamesDataUrl = "https://gist.githubusercontent.com/bmv1986/f5d7af4e17182a7e079d944f41f225ce/raw/2588ceb091657c1e731a287520d5d3ff211bba23/gistfile1.txt";
        
        private List<string> _libraries = new List<string> { @"D:\BOLGAMES" };
        private string _defaultLibrary = @"D:\BOLGAMES";
        private List<GameInfo> _games = new List<GameInfo>();
        private List<GameInfo> _filteredGames = new List<GameInfo>();
        private GameInfo _currentGame = null;
        private bool _isInstalled = false;
        private string _installLocation = "";
        private string _localVersion = "";
        private string _latestVersion = "";
        private bool _updateAvailable = false;
        private WebClient _webClient = null;
        private string _tempDownloadFile = null;
        private string _pendingDownloadUrl = null;
        private string _pendingFileName = null;
        private string _downloadingGameId = null;
        private string _pendingInstallPath = null;
        
        private List<string> _screenshots = new List<string>();
        private bool _isMaximized = false;
        private Rect _normalRect;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            await LoadGames();
            Task.Delay(2000).ContinueWith(_ => Dispatcher.Invoke(() => CheckForLauncherUpdate()));
        }

        private void CheckForLauncherUpdate()
        {
            try
            {
                AutoUpdater.Start("https://raw.githubusercontent.com/bmv1986/bolgames-launcher/main/update.xml");
                AutoUpdater.ReportErrors = false;
                AutoUpdater.ShowSkipButton = true;
                AutoUpdater.ShowRemindLaterButton = true;
            }
            catch { }
        }

        private void LoadSettings()
        {
            var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BOLGAMES");
            if (!Directory.Exists(appDataDir)) Directory.CreateDirectory(appDataDir);
            var settingsPath = Path.Combine(appDataDir, "settings.json");
            if (File.Exists(settingsPath))
            {
                try
                {
                    var settings = JObject.Parse(File.ReadAllText(settingsPath));
                    if (settings["libraries"] != null)
                    {
                        _libraries.Clear();
                        foreach (var lib in settings["libraries"])
                        {
                            var path = lib.ToString();
                            if (!path.EndsWith("BOLGAMES", StringComparison.OrdinalIgnoreCase)) path = Path.Combine(path, "BOLGAMES");
                            if (CanWriteToPath(path)) _libraries.Add(path);
                        }
                    }
                    if (settings["defaultLibrary"] != null)
                    {
                        var def = settings["defaultLibrary"].ToString();
                        if (_libraries.Contains(def, StringComparer.OrdinalIgnoreCase)) _defaultLibrary = def;
                    }
                }
                catch { }
            }
            if (_libraries.Count == 0) _libraries.Add(_defaultLibrary);
            foreach (var lib in _libraries.ToList())
            {
                try { if (!Directory.Exists(lib)) Directory.CreateDirectory(lib); }
                catch { _libraries.Remove(lib); }
            }
        }

        private void SaveSettings()
        {
            var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BOLGAMES");
            if (!Directory.Exists(appDataDir)) Directory.CreateDirectory(appDataDir);
            var settingsPath = Path.Combine(appDataDir, "settings.json");
            var settings = new JObject { ["libraries"] = new JArray(_libraries), ["defaultLibrary"] = _defaultLibrary };
            File.WriteAllText(settingsPath, settings.ToString());
        }

        private bool CanWriteToPath(string path)
        {
            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                var testFile = Path.Combine(path, ".test_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch { return false; }
        }

        private async Task LoadGames()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "BOLGAMES");
                var json = await client.GetStringAsync(GamesDataUrl);
                var data = JObject.Parse(json);
                _games.Clear();
                foreach (var item in data["games"])
                {
                    _games.Add(new GameInfo
                    {
                        Id = item["id"]?.ToString() ?? "",
                        Title = item["title"]?.ToString() ?? "Без названия",
                        ShortDescription = item["shortDescription"]?.ToString() ?? "",
                        Description = item["description"]?.ToString() ?? "",
                        News = item["news"]?.ToString() ?? "",
                        CoverImage = item["coverImage"]?.ToString() ?? "",
                        BannerImage = item["bannerImage"]?.ToString() ?? "",
                        GitHubRepo = item["githubRepo"]?.ToString() ?? "",
                        DownloadUrl = item["downloadUrl"]?.ToString() ?? "",
                        LatestVersion = item["latestVersion"]?.ToString() ?? "0.0.0",
                        FilePattern = item["filePattern"]?.ToString() ?? ".exe|.zip",
                        Executable = item["executable"]?.ToString() ?? "",
                        InstallSize = item["installSize"]?.ToString() ?? "?",
                        Developer = item["developer"]?.ToString() ?? "Неизвестно",
                        Publisher = item["publisher"]?.ToString() ?? "Неизвестно",
                        ReleaseDate = item["releaseDate"]?.ToString() ?? "Неизвестно",
                        Tags = item["tags"] != null ? string.Join(",", item["tags"].Select(t => t.ToString())) : "",
                        Screenshots = item["screenshots"] != null ? item["screenshots"].Select(s => s.ToString()).Where(s => !string.IsNullOrEmpty(s)).ToList() : new List<string>()
                    });
                }
                _filteredGames = new List<GameInfo>(_games);
                RenderGamesList();
                if (_games.Count > 0) SelectGame(_games[0]);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка чтения JSON:\n\n" + ex.Message + "\n\nПроверьте консоль или формат файла.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string FindGameInLibraries(GameInfo game)
        {
            foreach (var lib in _libraries)
            {
                var gameDir = Path.Combine(lib, game.Id);
                var exePath = Path.Combine(gameDir, game.Executable);
                if (File.Exists(exePath)) return gameDir;
            }
            return null;
        }

        private bool IsGameRunning(GameInfo game)
        {
            if (game == null) return false;
            try { return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(game.Executable)).Length > 0; }
            catch { return false; }
        }

        private void RenderGamesList()
        {
            GamesList.Children.Clear();
            foreach (var game in _filteredGames)
            {
                var gameDir = FindGameInLibraries(game);
                var isInstalled = gameDir != null;
                Color borderColor = Color.FromRgb(42, 71, 94);
                double borderWidth = 1;
                if (_downloadingGameId == game.Id) { borderColor = Color.FromRgb(102, 192, 244); borderWidth = 2; }
                else if (IsGameRunning(game)) { borderColor = Color.FromRgb(92, 126, 16); borderWidth = 2; }
                
                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(27, 40, 56)),
                    BorderBrush = new SolidColorBrush(borderColor),
                    BorderThickness = new Thickness(borderWidth),
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(10, 5, 10, 5),
                    Padding = new Thickness(10),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                card.MouseLeftButtonUp += (s, e) => SelectGame(game);
                card.MouseEnter += (s, e) => card.Background = new SolidColorBrush(Color.FromRgb(42, 71, 94));
                card.MouseLeave += (s, e) => card.Background = new SolidColorBrush(Color.FromRgb(27, 40, 56));

                var stack = new StackPanel { Orientation = Orientation.Horizontal };
                var imageBorder = new Border { Width = 60, Height = 80, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 10, 0), Background = new SolidColorBrush(Color.FromRgb(42, 71, 94)) };

                if (!string.IsNullOrEmpty(game.CoverImage))
                {
                    try { imageBorder.Child = new Image { Source = new BitmapImage(new Uri(game.CoverImage)), Stretch = Stretch.UniformToFill }; } catch { }
                }
                else { imageBorder.Child = new TextBlock { Text = "🎮", FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; }
                stack.Children.Add(imageBorder);

                var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                infoStack.Children.Add(new TextBlock { Text = game.Title, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 5) });
                infoStack.Children.Add(new TextBlock { Text = game.ShortDescription, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(143, 152, 160)), TextWrapping = TextWrapping.Wrap, MaxWidth = 160 });

                string statusText = "";
                Color statusColor = Color.FromRgb(143, 152, 160);
                if (_downloadingGameId == game.Id) { statusText = "⬇ Скачивается..."; statusColor = Color.FromRgb(102, 192, 244); }
                else if (IsGameRunning(game)) { statusText = "▶ Запущена"; statusColor = Color.FromRgb(92, 126, 16); }
                else if (isInstalled)
                {
                    var versionFile = Path.Combine(gameDir, "version.txt");
                    var localVer = File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : "?";
                    statusText = "✓ v" + localVer;
                    statusColor = Color.FromRgb(92, 126, 16);
                }

                if (!string.IsNullOrEmpty(statusText)) infoStack.Children.Add(new TextBlock { Text = statusText, FontSize = 10, Foreground = new SolidColorBrush(statusColor), Margin = new Thickness(0, 5, 0, 0) });
                stack.Children.Add(infoStack);
                card.Child = stack;
                GamesList.Children.Add(card);
            }
        }

        private void SelectGame(GameInfo game)
        {
            _currentGame = game;
            SettingsPage.Visibility = Visibility.Collapsed;
            GamePage.Visibility = Visibility.Visible;
            InstallPathDialog.Visibility = Visibility.Collapsed;
            MovePathDialog.Visibility = Visibility.Collapsed;

            if (!string.IsNullOrEmpty(game.BannerImage)) { try { BannerImage.Source = new BitmapImage(new Uri(game.BannerImage)); } catch { BannerImage.Source = null; } }
            else { BannerImage.Source = null; }

            GameTitle.Text = game.Title;
            GameDescription.Text = game.Description;
            GameNews.Text = game.News;
            GameDeveloper.Text = game.Developer;
            GamePublisher.Text = game.Publisher;
            GameReleaseDate.Text = game.ReleaseDate;
            GameSize.Text = game.InstallSize;

            _installLocation = FindGameInLibraries(game);
            _isInstalled = _installLocation != null;
            _localVersion = _isInstalled && File.Exists(Path.Combine(_installLocation, "version.txt")) ? File.ReadAllText(Path.Combine(_installLocation, "version.txt")).Trim() : "не установлена";
            GameLocation.Text = _isInstalled ? _installLocation : "Не установлена";

            _latestVersion = null; _pendingDownloadUrl = null; _pendingFileName = null; _updateAvailable = false;
            
            // Теги-бейджи (безопасная версия)
            GameTagsPanel.Children.Clear();
            if (!string.IsNullOrEmpty(game.Tags))
            {
                var tags = game.Tags.Split(',');
                foreach (var tag in tags)
                {
                    var cleanTag = tag.Trim();
                    if (!string.IsNullOrEmpty(cleanTag))
                    {
                        var border = new Border { Background = new SolidColorBrush(Color.FromRgb(102, 192, 244)), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0) };
                        var textBlock = new TextBlock { Text = cleanTag, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
                        border.Child = textBlock;
                        GameTagsPanel.Children.Add(border);
                    }
                }
            }
            
            // Горизонтальная лента скриншотов
            _screenshots = game.Screenshots ?? new List<string>();
            ScreenshotsPanel.Children.Clear();
            if (_screenshots.Count > 0)
            {
                ScreenshotsBorder.Visibility = Visibility.Visible;
                foreach (var screenshotUrl in _screenshots)
                {
                    try
                    {
                        var image = new System.Windows.Controls.Image
                        {
                            Height = 150,
                            Width = 267,
                            Stretch = Stretch.UniformToFill,
                            Margin = new Thickness(0, 0, 10, 0)
                        };
                        image.Source = new BitmapImage(new Uri(screenshotUrl));
                        ScreenshotsPanel.Children.Add(image);
                    }
                    catch { }
                }
            }
            else
            {
                ScreenshotsBorder.Visibility = Visibility.Collapsed;
            }

            Task.Run(() =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(game.DownloadUrl))
                    {
                        _pendingDownloadUrl = game.DownloadUrl;
                        _latestVersion = game.LatestVersion ?? "0.0.0";
                        _pendingFileName = System.IO.Path.GetFileName(new Uri(game.DownloadUrl).LocalPath);
                    }
                }
                catch { }
                Dispatcher.Invoke(() => UpdateGamePage());
            });
        }

        private void UpdateGamePage()
        {
            GameVersion.Text = _isInstalled ? "v" + _localVersion : "Не установлена";
            if (_downloadingGameId == _currentGame?.Id)
            {
                DownloadProgressPanel.Visibility = Visibility.Visible;
                CancelDownloadBtn.Visibility = Visibility.Visible;
                SkipBtn.Visibility = _isInstalled ? Visibility.Visible : Visibility.Collapsed;
                InstallBtn.Visibility = Visibility.Collapsed;
                MoveBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                DownloadProgressPanel.Visibility = Visibility.Collapsed;
                CancelDownloadBtn.Visibility = Visibility.Collapsed;
                SkipBtn.Visibility = Visibility.Collapsed;
                InstallBtn.Visibility = Visibility.Visible;
                if (_isInstalled)
                {
                    DeleteBtn.Visibility = Visibility.Visible;
                    if (_latestVersion != null && _localVersion != _latestVersion) { _updateAvailable = true; InstallBtn.Content = "🔄 ОБНОВИТЬ до v" + _latestVersion; }
                    else { _updateAvailable = false; InstallBtn.Content = "▶ ИГРАТЬ"; }
                    MoveBtn.Visibility = _libraries.Any(l => !l.Equals(_installLocation, StringComparison.OrdinalIgnoreCase)) ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    DeleteBtn.Visibility = Visibility.Collapsed;
                    MoveBtn.Visibility = Visibility.Collapsed;
                    _updateAvailable = false;
                    InstallBtn.Content = " УСТАНОВИТЬ";
                }
            }
            InstallBtn.IsEnabled = true; DeleteBtn.IsEnabled = true; MoveBtn.IsEnabled = true;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchBox.Text.ToLowerInvariant();
            _filteredGames = string.IsNullOrEmpty(searchText) ? new List<GameInfo>(_games) : _games.Where(g => g.Title.ToLowerInvariant().Contains(searchText) || g.ShortDescription.ToLowerInvariant().Contains(searchText)).ToList();
            RenderGamesList();
        }

        private void BackFromSettings_Click(object sender, RoutedEventArgs e) { SettingsPage.Visibility = Visibility.Collapsed; GamePage.Visibility = Visibility.Visible; }
        
        private void SettingsBtn_Click(object sender, RoutedEventArgs e) 
        { 
            GamePage.Visibility = Visibility.Collapsed; 
            SettingsPage.Visibility = Visibility.Visible; 
            RenderLibrariesList();
            var assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            AboutVersion.Text = assemblyVersion != null ? assemblyVersion.ToString() : "Неизвестно";
        }

        private void RenderLibrariesList()
        {
            LibrariesList.Children.Clear();
            foreach (var lib in _libraries)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                string driveInfo = "";
                try { var drive = new System.IO.DriveInfo(lib.Substring(0, 2)); if (drive.IsReady) driveInfo = drive.Name + " (" + drive.VolumeLabel + ") — свободно " + (drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0).ToString("F1") + " ГБ"; } catch { }
                var pathText = new TextBlock { Text = lib + "  —  " + driveInfo, Foreground = Brushes.White, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(pathText, 0); row.Children.Add(pathText);
                var removeBtn = new Button { Content = "✕", Width = 30, Height = 30, Padding = new Thickness(0), Style = (Style)FindResource("SecondaryButton"), IsEnabled = _libraries.Count > 1 };
                var libToRemove = lib;
                removeBtn.Click += (s, e) => RemoveLibrary(libToRemove);
                Grid.SetColumn(removeBtn, 1); row.Children.Add(removeBtn);
                LibrariesList.Children.Add(row);
            }
        }

        private void AddLibraryBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "Выберите папку для библиотеки BOLGAMES", ShowNewFolderButton = true };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var newPath = Path.Combine(dialog.SelectedPath, "BOLGAMES");
                if (_libraries.Any(l => l.Equals(newPath, StringComparison.OrdinalIgnoreCase))) { MessageBox.Show("Эта папка уже добавлена.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information); return; }
                if (!CanWriteToPath(newPath)) { WarningBorder.Visibility = Visibility.Visible; WarningText.Text = "⚠ Нет прав на запись в:\n" + newPath; return; }
                _libraries.Add(newPath); SaveSettings(); RenderLibrariesList(); WarningBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void RemoveLibrary(string path)
        {
            if (_libraries.Count <= 1) { MessageBox.Show("Нельзя удалить последнюю библиотеку.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var gamesInLibrary = _games.Where(g => Directory.Exists(Path.Combine(path, g.Id))).ToList();
            if (gamesInLibrary.Count > 0)
            {
                if (MessageBox.Show("В этой папке есть игры:\n" + string.Join("\n", gamesInLibrary.Select(g => "• " + g.Title)) + "\n\nУдалить их?", "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                foreach (var game in gamesInLibrary) try { if (Directory.Exists(Path.Combine(path, game.Id))) Directory.Delete(Path.Combine(path, game.Id), true); } catch { }
            }
            _libraries.Remove(path);
            if (_defaultLibrary.Equals(path, StringComparison.OrdinalIgnoreCase) && _libraries.Count > 0) _defaultLibrary = _libraries[0];
            SaveSettings(); RenderLibrariesList();
        }

        private void InstallBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGame == null) return;
            if (_isInstalled)
            {
                if (_updateAvailable)
                {
                    if (IsGameRunning(_currentGame)) { MessageBox.Show("Игра запущена! Закройте её.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                    _pendingInstallPath = _installLocation; StartDownload();
                }
                else LaunchGame();
            }
            else ShowInstallPathDialog();
        }

        private void ShowInstallPathDialog()
        {
            InstallPathSelector.Items.Clear();
            foreach (var lib in _libraries)
            {
                string driveInfo = "";
                try { var drive = new System.IO.DriveInfo(lib.Substring(0, 2)); if (drive.IsReady) driveInfo = " — свободно " + (drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0).ToString("F1") + " ГБ"; } catch { }
                var item = new ComboBoxItem { Content = lib + driveInfo, Tag = lib };
                InstallPathSelector.Items.Add(item);
                if (lib.Equals(_defaultLibrary, StringComparison.OrdinalIgnoreCase)) InstallPathSelector.SelectedItem = item;
            }
            InstallPathDialogText.Text = "Игра \"" + _currentGame.Title + "\" будет установлена в выбранную папку.";
            InstallPathDialog.Visibility = Visibility.Visible;
        }

        private void CancelInstallPath_Click(object sender, RoutedEventArgs e) { InstallPathDialog.Visibility = Visibility.Collapsed; }
        private void ConfirmInstallPath_Click(object sender, RoutedEventArgs e)
        {
            if (InstallPathSelector.SelectedItem is ComboBoxItem selected) { _pendingInstallPath = selected.Tag.ToString(); InstallPathDialog.Visibility = Visibility.Collapsed; StartDownload(); }
        }

        private void MoveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGame == null || !_isInstalled) return;
            if (IsGameRunning(_currentGame)) { MessageBox.Show("Игра запущена! Закройте её перед перемещением.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            ShowMovePathDialog();
        }

        private void ShowMovePathDialog()
        {
            MovePathSelector.Items.Clear();
            foreach (var lib in _libraries)
            {
                if (lib.Equals(_installLocation, StringComparison.OrdinalIgnoreCase)) continue;
                string driveInfo = "";
                try { var drive = new System.IO.DriveInfo(lib.Substring(0, 2)); if (drive.IsReady) driveInfo = " — свободно " + (drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0).ToString("F1") + " ГБ"; } catch { }
                MovePathSelector.Items.Add(new ComboBoxItem { Content = lib + driveInfo, Tag = lib });
            }
            if (MovePathSelector.Items.Count > 0) MovePathSelector.SelectedIndex = 0;
            MovePathDialogText.Text = "Игра \"" + _currentGame.Title + "\" будет перемещена из:\n" + _installLocation;
            MovePathDialog.Visibility = Visibility.Visible;
        }

        private void CancelMovePath_Click(object sender, RoutedEventArgs e) { MovePathDialog.Visibility = Visibility.Collapsed; }
        private async void ConfirmMovePath_Click(object sender, RoutedEventArgs e)
        {
            if (MovePathSelector.SelectedItem is ComboBoxItem selected) { MovePathDialog.Visibility = Visibility.Collapsed; await StartMove(selected.Tag.ToString()); }
        }

        private async Task StartMove(string targetLibrary)
        {
            var sourceDir = _installLocation;
            var targetDir = Path.Combine(targetLibrary, _currentGame.Id);
            if (Directory.Exists(targetDir) && MessageBox.Show("В целевой папке уже есть файлы. Перезаписать?", "Внимание", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            
            _downloadingGameId = _currentGame.Id;
            DownloadProgressPanel.Visibility = Visibility.Visible; CancelDownloadBtn.Visibility = Visibility.Visible; SkipBtn.Visibility = Visibility.Collapsed;
            InstallBtn.Visibility = Visibility.Collapsed; MoveBtn.Visibility = Visibility.Collapsed;
            DownloadProgress.Value = 0; DownloadProgressText.Text = "Подготовка к перемещению...";
            RenderGamesList();
            
            try
            {
                var sourceSize = GetDirectorySize(sourceDir);
                long copiedBytes = 0;
                await Task.Run(() => CopyDirectoryWithProgress(sourceDir, targetDir, (copied) => {
                    copiedBytes = copied;
                    Dispatcher.Invoke(() => {
                        if (sourceSize > 0) {
                            var percent = (int)((copiedBytes * 100) / sourceSize);
                            DownloadProgress.Value = Math.Min(100, percent);
                            DownloadProgressText.Text = "Перемещение: " + percent + "% (" + (copiedBytes/1024.0/1024.0).ToString("F1") + " / " + (sourceSize/1024.0/1024.0).ToString("F1") + " МБ)";
                        }
                    });
                }));
                DownloadProgressText.Text = "Удаление старой копии..."; DownloadProgress.Value = 100; await Task.Delay(500);
                if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, true);
                SystemSounds.Asterisk.Play();
                DownloadProgressText.Text = "✅ Успешно перемещено!";
                await Task.Delay(2000);
                _downloadingGameId = null;
                DownloadProgressPanel.Visibility = Visibility.Collapsed; CancelDownloadBtn.Visibility = Visibility.Collapsed;
                InstallBtn.Visibility = Visibility.Visible;
                RenderGamesList(); SelectGame(_currentGame);
            }
            catch (Exception ex)
            {
                _downloadingGameId = null;
                DownloadProgressPanel.Visibility = Visibility.Collapsed; CancelDownloadBtn.Visibility = Visibility.Collapsed;
                InstallBtn.Visibility = Visibility.Visible; MoveBtn.Visibility = Visibility.Visible;
                MessageBox.Show("Ошибка при перемещении:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                RenderGamesList(); UpdateGamePage();
            }
        }

        private long GetDirectorySize(string path) { long size = 0; try { foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories)) size += new FileInfo(file).Length; } catch { } return size; }

        private void CopyDirectoryWithProgress(string sourceDir, string targetDir, Action<long> progressCallback)
        {
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
            long totalCopied = 0;
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var targetFile = Path.Combine(targetDir, Path.GetRelativePath(sourceDir, file));
                var targetFileDir = Path.GetDirectoryName(targetFile);
                if (!Directory.Exists(targetFileDir)) Directory.CreateDirectory(targetFileDir);
                File.Copy(file, targetFile, true);
                totalCopied += new FileInfo(file).Length;
                progressCallback(totalCopied);
            }
        }

        private void StartDownload()
        {
            if (string.IsNullOrEmpty(_pendingDownloadUrl)) { MessageBox.Show("Не удалось получить ссылку.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            InstallBtn.IsEnabled = false; DeleteBtn.IsEnabled = false; MoveBtn.IsEnabled = false;
            var gameDir = Path.Combine(_pendingInstallPath, _currentGame.Id);
            try { if (!Directory.Exists(gameDir)) Directory.CreateDirectory(gameDir); }
            catch (Exception ex) { MessageBox.Show("Не удалось создать папку:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); InstallBtn.IsEnabled = true; DeleteBtn.IsEnabled = true; MoveBtn.IsEnabled = true; return; }

            _tempDownloadFile = Path.Combine(gameDir, "temp_download.tmp");
            _downloadingGameId = _currentGame.Id;
            DownloadProgressPanel.Visibility = Visibility.Visible; CancelDownloadBtn.Visibility = Visibility.Visible; SkipBtn.Visibility = _isInstalled ? Visibility.Visible : Visibility.Collapsed;
            InstallBtn.Visibility = Visibility.Collapsed; MoveBtn.Visibility = Visibility.Collapsed;
            DownloadProgress.Value = 0; DownloadProgressText.Text = "Подготовка к скачиванию " + _pendingFileName + "...";
            RenderGamesList();

            _webClient = new WebClient();
            _webClient.Headers.Add("User-Agent", "BOLGAMES");
            _webClient.DownloadProgressChanged += (s, args) => Dispatcher.Invoke(() => {
                DownloadProgress.Value = args.ProgressPercentage;
                DownloadProgressText.Text = "Скачивание: " + args.ProgressPercentage + "% (" + (args.BytesReceived/1024.0/1024.0).ToString("F1") + " / " + (args.TotalBytesToReceive/1024.0/1024.0).ToString("F1") + " МБ)";
            });

            _webClient.DownloadFileCompleted += (s, args) => Dispatcher.Invoke(async () => {
                _downloadingGameId = null;
                DownloadProgressPanel.Visibility = Visibility.Collapsed; CancelDownloadBtn.Visibility = Visibility.Collapsed; SkipBtn.Visibility = Visibility.Collapsed;
                InstallBtn.Visibility = Visibility.Visible; MoveBtn.Visibility = Visibility.Visible;
                if (args.Cancelled) { DownloadProgressText.Text = "Отменено."; if (File.Exists(_tempDownloadFile)) try { File.Delete(_tempDownloadFile); } catch { } RenderGamesList(); UpdateGamePage(); return; }
                if (args.Error != null) { MessageBox.Show("Ошибка:\n" + args.Error.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); RenderGamesList(); UpdateGamePage(); return; }

                DownloadProgressText.Text = "Установка..."; DownloadProgress.Value = 100; await Task.Delay(500);
                try
                {
                    if (_pendingFileName.ToLowerInvariant().EndsWith(".zip")) await Task.Run(() => ZipFile.ExtractToDirectory(_tempDownloadFile, gameDir, true));
                    else { var destFile = Path.Combine(gameDir, _pendingFileName); if (File.Exists(destFile)) File.Delete(destFile); File.Move(_tempDownloadFile, destFile); }
                    if (File.Exists(_tempDownloadFile)) File.Delete(_tempDownloadFile);
                    File.WriteAllText(Path.Combine(gameDir, "version.txt"), _latestVersion);
                    SystemSounds.Asterisk.Play();
                    DownloadProgressText.Text = "✅ Успешно установлено/обновлено!";
                    await Task.Delay(2000);
                    RenderGamesList(); SelectGame(_currentGame);
                }
                catch (Exception ex) { MessageBox.Show("Ошибка установки:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); RenderGamesList(); UpdateGamePage(); }
            });

            try { _webClient.DownloadFileTaskAsync(new Uri(_pendingDownloadUrl), _tempDownloadFile); }
            catch (Exception ex) { _downloadingGameId = null; MessageBox.Show("Ошибка: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); RenderGamesList(); UpdateGamePage(); }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e) { if (MessageBox.Show("Отменить?", "Отмена", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) { _webClient?.CancelAsync(); DownloadProgressText.Text = "Отмена..."; CancelDownloadBtn.IsEnabled = false; SkipBtn.IsEnabled = false; } }
        private void SkipBtn_Click(object sender, RoutedEventArgs e) { if (MessageBox.Show("Пропустить обновление и играть?", "Пропустить", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) { _webClient?.CancelAsync(); if (File.Exists(_tempDownloadFile)) try { File.Delete(_tempDownloadFile); } catch { } _downloadingGameId = null; RenderGamesList(); LaunchGame(); } }
        
        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGame == null || !_isInstalled) return;
            if (IsGameRunning(_currentGame)) { MessageBox.Show("Нельзя удалить запущенную игру.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (MessageBox.Show("Удалить игру \"" + _currentGame.Title + "\"?\n\n" + _installLocation, "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try { if (Directory.Exists(_installLocation)) Directory.Delete(_installLocation, true); MessageBox.Show("Игра удалена.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information); SelectGame(_currentGame); }
                catch (Exception ex) { MessageBox.Show("Не удалось удалить:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void LaunchGame()
        {
            if (_currentGame == null || !_isInstalled) return;
            var exePath = Path.Combine(_installLocation, _currentGame.Executable);
            if (!File.Exists(exePath)) { MessageBox.Show("Файл не найден:\n" + exePath, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            var exeName = Path.GetFileNameWithoutExtension(_currentGame.Executable);
            Process[] runningProcesses; try { runningProcesses = Process.GetProcessesByName(exeName); } catch { runningProcesses = Array.Empty<Process>(); }
            if (runningProcesses.Length > 0 && MessageBox.Show("Игра уже запущена! Запустить ещё одну копию?", "Внимание", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No) return;

            try
            {
                InstallBtn.Content = "⏳ ЗАПУСК..."; InstallBtn.IsEnabled = false;
                Process.Start(new ProcessStartInfo { FileName = exePath, WorkingDirectory = _installLocation, UseShellExecute = true });
                Task.Delay(3000).ContinueWith(_ => Dispatcher.Invoke(() => { RenderGamesList(); UpdateGamePage(); }));
            }
            catch (Exception ex) { MessageBox.Show("Не удалось запустить:\n" + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); UpdateGamePage(); }
        }

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e) { await LoadGames(); }
        
        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) { ToggleMaximize(); }
            else { this.DragMove(); }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) { this.WindowState = System.Windows.WindowState.Minimized; }
        private void Maximize_Click(object sender, RoutedEventArgs e) { ToggleMaximize(); }

        private void ToggleMaximize()
        {
            if (_isMaximized)
            {
                this.WindowState = System.Windows.WindowState.Normal;
                this.Left = _normalRect.Left; this.Top = _normalRect.Top;
                this.Width = _normalRect.Width; this.Height = _normalRect.Height;
                _isMaximized = false;
                MaximizeBtn.Content = "🗖";
            }
            else
            {
                _normalRect = new Rect(this.Left, this.Top, this.Width, this.Height);
                var workArea = SystemParameters.WorkArea;
                this.Left = workArea.Left; this.Top = workArea.Top;
                this.Width = workArea.Width; this.Height = workArea.Height;
                _isMaximized = true;
                MaximizeBtn.Content = "🗗";
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) { this.Close(); }
    }

    public class GameInfo
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string ShortDescription { get; set; }
        public string Description { get; set; }
        public string News { get; set; }
        public string CoverImage { get; set; }
        public string BannerImage { get; set; }
        public string GitHubRepo { get; set; }
        public string DownloadUrl { get; set; }
        public string LatestVersion { get; set; }
        public string FilePattern { get; set; }
        public string Executable { get; set; }
        public string InstallSize { get; set; }
        public string Developer { get; set; }
        public string Publisher { get; set; }
        public string ReleaseDate { get; set; }
        public string Tags { get; set; }
        public List<string> Screenshots { get; set; } = new List<string>();
    }
}