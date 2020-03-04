using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Founder2
{
    static class MainFormViewModel
    {
        public static TreeNode NodeCollection;
        public static string SearchPath { get; set; }
        public static bool SearchingInWork { get; set; }

        public static void PrepareNodes(string filePath)
        {
            TreeNode t= NodeCollection.Nodes.Find(Path.GetDirectoryName(filePath), true).FirstOrDefault();
            if (t != null)
            {
                t.Nodes.Add(new TreeNode { Name = filePath, Text = Path.GetFileName(filePath) }); }
            else
            {
            string _searchPath = SearchPath;
            Stack<string> Names = new Stack<string>();
            var adress = filePath.Split('\\');
            Names.Push(filePath);
            string fullName = filePath;
            for (int i = adress.Length-2; i >= 0; i--)
            {
                fullName = Path.GetDirectoryName(fullName);
                Names.Push(fullName);
            }
            fillTree(Names, NodeCollection);
            
            }
        }
        private static void fillTree(Stack<string> names, TreeNode nodeToAddTo)
        {
            TreeNode currentNode = new TreeNode();
            currentNode.Name = names.Pop();
            currentNode.Text = Path.GetFileName(currentNode.Name) == "" ? currentNode.Name:Path.GetFileName(currentNode.Name);
            TreeNode nextNode = nodeToAddTo.Nodes.Find(currentNode.Name, false).FirstOrDefault();
            if (nextNode != null)
            {
                if (names.Count > 0) fillTree(names, nextNode);
            }
            else
            {
                nodeToAddTo.Nodes.Add(currentNode);
                if (names.Count > 0) fillTree(names, currentNode);
            }
        }
        /// <summary>
            /// Заполнение дочерних узлов дочерними узлами развёртываемого узала
            /// </summary>
            /// <param name="tree">Заполняемое дерево</param>
            /// <param name="treeNode">Разворачиваемая нода</param>
            /// <returns></returns>
        public static void fillExpanded(TreeView tree, TreeNode treeNode, ManualResetEvent busy)
        { 
            //Проверяем не скрыт ли узел, если проверять раскрытие то свернув корень дочерние узлы будут продолжать обновляться
            var z = tree.Invoke((Func<bool>)(() => treeNode.FirstNode.IsVisible));
            foreach (TreeNode item in treeNode.Nodes)
            {
                TreeNode t = NodeCollection.Nodes.Find(item.Name, true).FirstOrDefault();
                foreach (TreeNode child in t.Nodes)
                {
                    TreeNode f = item.Nodes.Find(child.Name, false).FirstOrDefault();
                    if (f == null)
                    {
                        tree.BeginInvoke((Action)(() => item.Nodes.Add(new TreeNode { Name = child.Name, Text = child.Text })));
                    }
                }
            }
            busy.WaitOne();
            Thread.Sleep(100);
            if (SearchingInWork & (bool)z)
            {
                Task.Run(() => fillExpanded(tree, treeNode, busy));
            }
        }
        /// <summary>
        /// Заполнение первых двух уровней дерева. И отслеживание изменений в коллекции найденых файлов.
        /// </summary>
        /// <returns></returns>
        public static void fillRootNodes(TreeView tree, ManualResetEvent busy)
        {
            busy.WaitOne();
            //if (tree.Nodes.Count == 0) tree.BeginInvoke((Action)(() => tree.Nodes.Add(new TreeNode { Name = NodeCollection.Name, Text = NodeCollection.Text })));//Лишняя проверка в цикле так как нода будет пустой до первого заполнения
            foreach (TreeNode item in NodeCollection?.Nodes)
            {
                TreeNode t = tree.Nodes[0].Nodes.Find(item.Name, false).FirstOrDefault();
                if (t == null)
                {
                    tree.BeginInvoke((Action)(() => tree.Nodes[0].Nodes.Add(new TreeNode { Name = item.Name, Text = item.Text })));
                }
                
            }
            Thread.Sleep(500);
            if (SearchingInWork)
            {
                Task.Run(() => fillRootNodes(tree, busy));
            }
        }

    }
}
