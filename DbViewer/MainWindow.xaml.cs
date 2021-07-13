﻿using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DbViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private readonly HashSet<SqliteConnection> _activeConnections = new();

        private void AboutMenu_Click(object sender, RoutedEventArgs e)
        {
            new AboutWindow()
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.Show();
        }

        private void NavigatorItem_Selected(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource == e.Source)
            {
                e.Handled = true;
                return;
            }

            if (e.OriginalSource is TreeViewItem tvi && e.Source is TreeViewItem parent)
            {
                SqliteCommand command = new()
                {
                    CommandText = $"SELECT * FROM \'{tvi.Header}\'",
                    Connection = parent.DataContext as SqliteConnection
                };
                if (command.Connection == null)
                {
                    MessageBox.Show("Invalid database connection.");
                    return;
                }

                try
                {
                    command.Connection.Open();
                    DataViewer.DataContext = command;
                    using SqliteDataReader reader = command.ExecuteReader();
                    DataViewer.ItemsSource = reader.OfType<object>().ToList();
                }
                finally
                {
                    command.Connection.Close();
                }
            }
        }

        private void CloseConnection_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem node = ((FrameworkElement)sender).DataContext as TreeViewItem;
            SqliteConnection connection = node?.DataContext as SqliteConnection;
            _activeConnections.Remove(connection);
            connection?.Dispose();
            Navigator.Items.Remove(node);
            DataViewer.DataContext = DataViewer.ItemsSource = null;
            e.Handled = true;
        }

        private void ExitMenu_Click(object sender, RoutedEventArgs e) => Close();

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            foreach (SqliteConnection connection in _activeConnections)
            {
                connection.Dispose();
            }
        }

        private void OpenCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "SQLite 数据库文件 (*.db)|*.db"
            };

            if (openFileDialog.ShowDialog(this) != true)
                return;

            SqliteConnectionStringBuilder connectionStringBuilder = new()
            {
                DataSource = openFileDialog.FileName
            };
            SqliteConnection connection = new()
            {
                ConnectionString = connectionStringBuilder.ToString()
            };
            using SqliteCommand command = new()
            {
                CommandText = "SELECT sm.name FROM sqlite_master sm WHERE sm.type='table';",
                Connection = connection
            };
            try
            {
                connection.Open();
                _activeConnections.Add(connection);
                SqliteDataReader reader = command.ExecuteReader();
                List<string> tableList = reader.OfType<IDataRecord>()
                    .Select(x => x.GetString(0))
                    .OrderBy(x => x).ToList();
                TreeViewItem node = new()
                {
                    Header = openFileDialog.SafeFileName,
                    DataContext = connection,
                    ItemsSource = tableList,
                    ContextMenu = Navigator.Resources["ItemMenu"] as ContextMenu
                };
                if (node.ContextMenu != null)
                {
                    node.ContextMenu.DataContext = node;
                }

                _ = Navigator.Items.Add(node);
            }
            catch (SqliteException exception)
            {
                _ = MessageBox.Show(this,
                    $"SQLite 错误：\n{exception.Message}\n{exception.StackTrace}",
                    "SQLite 错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception exception)
            {
                _ = MessageBox.Show(this,
                    $"未知错误：\n{exception.Message}\n{exception.StackTrace}",
                    "未知错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                connection.Close();
            }
        }

        private void RefreshCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (DataViewer.ItemsSource != null)
            {
                DataViewer.ItemsSource = null;
                if (DataViewer.DataContext is not SqliteCommand command)
                    return;
                try
                {
                    command.Connection.Open();
                    using SqliteDataReader reader = command.ExecuteReader();
                    DataViewer.ItemsSource = reader.OfType<object>().ToList();
                }
                finally
                {
                    command.Connection.Close();
                }
            }
        }
    }
}