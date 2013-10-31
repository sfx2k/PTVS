﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using EnvDTE;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.SharedProject;
using TestUtilities.UI;
using MSBuild = Microsoft.Build.Evaluation;

namespace VisualStudioToolsUITests {
    [TestClass]
    public class LinkedFileTests : SharedProjectTest {
        private static ProjectDefinition LinkedFiles(ProjectType projectType) {
            return new ProjectDefinition(
                "LinkedFiles",
                projectType,
                ItemGroup(
                    Folder("MoveToFolder"),
                    Folder("FolderWithAFile"),
                    Folder("Foo"),
                    Folder("..\\LinkedFilesDir", isExcluded: true),
                    Folder("AlreadyLinkedFolder"),

                    Compile("Program"),
                    Compile("..\\ImplicitLinkedFile"),
                    Compile("..\\ExplicitLinkedFile")
                        .Link("ExplicitDir\\ExplicitLinkedFile"),
                    Compile("..\\ExplicitLinkedFileWrongFilename")
                        .Link("ExplicitDir\\Blah"),
                    Compile("..\\MovedLinkedFile"),
                    Compile("..\\MovedLinkedFileOpen"),
                    Compile("..\\MovedLinkedFileOpenEdit"),
                    Compile("..\\FileNotInProject"),
                    Compile("..\\DeletedLinkedFile"),
                    Compile("LinkedInModule")
                        .Link("Foo\\LinkedInModule"),
                    Compile("SaveAsCreateLink"),
                    Compile("..\\SaveAsCreateFile"),
                    Compile("..\\SaveAsCreateFileNewDirectory"),
                    Compile("FolderWithAFile\\ExistsOnDiskAndInProject"),
                    Compile("FolderWithAFile\\ExistsInProjectButNotOnDisk", isMissing: true),
                    Compile("FolderWithAFile\\ExistsOnDiskButNotInProject"),
                    Compile("..\\LinkedFilesDir\\SomeLinkedFile")
                        .Link("Bar\\SomeLinkedFile"),
                    Compile("..\\RenamedLinkFile")
                        .Link("Foo\\NewNameForLinkFile"),
                    Compile("..\\BadLinkPath")
                        .Link("..\\BadLinkPathFolder\\BadLinkPath"),
                    Compile("..\\RootedLinkIgnored")
                        .Link("C:\\RootedLinkIgnored"),
                    Compile("C:\\RootedIncludeIgnored", isMissing: true)
                        .Link("RootedIncludeIgnored"),
                    Compile("Foo\\AddExistingInProjectDirButNotInProject"),
                    Compile("..\\ExistingItem", isExcluded: true),
                    Compile("..\\ExistsInProjectButNotOnDisk", isExcluded: true),
                    Compile("..\\ExistsOnDiskAndInProject", isExcluded: true),
                    Compile("..\\ExistsOnDiskButNotInProject", isExcluded: true)
                )
            );
        }

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RenameLinkedNode() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {
                    // implicitly linked node
                    var projectNode = solution.FindItem("LinkedFiles", "ImplicitLinkedFile" + projectType.CodeExtension);
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    try {
                        solution.App.Dte.ExecuteCommand("File.Rename");
                        Assert.Fail("Should have failed to rename");
                    } catch (Exception e) {
                        Debug.WriteLine(e.ToString());
                    }


                    // explicitly linked node
                    var explicitLinkedFile = solution.FindItem("LinkedFiles", "ExplicitDir", "ExplicitLinkedFile" + projectType.CodeExtension);
                    Assert.IsNotNull(explicitLinkedFile, "explicitLinkedFile");
                    AutomationWrapper.Select(explicitLinkedFile);

                    try {
                        solution.App.Dte.ExecuteCommand("File.Rename");
                        Assert.Fail("Should have failed to rename");
                    } catch (Exception e) {
                        Debug.WriteLine(e.ToString());
                    }

                    var autoItem = solution.Project.ProjectItems.Item("ImplicitLinkedFile" + projectType.CodeExtension);
                    try {
                        autoItem.Properties.Item("FileName").Value = "Foo";
                        Assert.Fail("Should have failed to rename");
                    } catch (TargetInvocationException tie) {
                        Assert.AreEqual(tie.InnerException.GetType(), typeof(InvalidOperationException));
                    }

                    autoItem = solution.Project.ProjectItems.Item("ExplicitDir").Collection.Item("ExplicitLinkedFile" + projectType.CodeExtension);
                    try {
                        autoItem.Properties.Item("FileName").Value = "Foo";
                        Assert.Fail("Should have failed to rename");
                    } catch (TargetInvocationException tie) {
                        Assert.AreEqual(tie.InnerException.GetType(), typeof(InvalidOperationException));
                    }
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveLinkedNode() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {

                    var projectNode = solution.FindItem("LinkedFiles", "MovedLinkedFile" + projectType.CodeExtension);
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    solution.App.Dte.ExecuteCommand("Edit.Cut");

                    var folderNode = solution.FindItem("LinkedFiles", "MoveToFolder");
                    AutomationWrapper.Select(folderNode);

                    solution.App.Dte.ExecuteCommand("Edit.Paste");

                    // item should have moved
                    var movedLinkedFile = solution.WaitForItem("LinkedFiles", "MoveToFolder", "MovedLinkedFile" + projectType.CodeExtension);
                    Assert.IsNotNull(movedLinkedFile, "movedLinkedFile");

                    // file should be at the same location
                    Assert.IsTrue(File.Exists(Path.Combine(solution.Directory, "MovedLinkedFile" + projectType.CodeExtension)));
                    Assert.IsFalse(File.Exists(Path.Combine(solution.Directory, "MoveToFolder\\MovedLinkedFile" + projectType.CodeExtension)));

                    // now move it back
                    AutomationWrapper.Select(movedLinkedFile);
                    solution.App.Dte.ExecuteCommand("Edit.Cut");

                    var originalFolder = solution.FindItem("LinkedFiles");
                    AutomationWrapper.Select(originalFolder);
                    solution.App.Dte.ExecuteCommand("Edit.Paste");

                    var movedLinkedFilePaste = solution.WaitForItem("LinkedFiles", "MovedLinkedFile" + projectType.CodeExtension);
                    Assert.IsNotNull(movedLinkedFilePaste, "movedLinkedFilePaste");

                    // and make sure we didn't mess up the path in the project file
                    MSBuild.Project buildProject = new MSBuild.Project(solution.Project.FullName);
                    bool found = false;
                    foreach (var item in buildProject.GetItems("Compile")) {
                        if (item.UnevaluatedInclude == "..\\MovedLinkedFile" + projectType.CodeExtension) {
                            found = true;
                            break;
                        }
                    }

                    Assert.IsTrue(found);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveLinkedNodeOpen() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {

                    var openWindow = solution.Project.ProjectItems.Item("MovedLinkedFileOpen" + projectType.CodeExtension).Open();
                    Assert.IsNotNull(openWindow, "openWindow");

                    var projectNode = solution.FindItem("LinkedFiles", "MovedLinkedFileOpen" + projectType.CodeExtension);

                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    solution.App.Dte.ExecuteCommand("Edit.Cut");

                    var folderNode = solution.FindItem("LinkedFiles", "MoveToFolder");
                    AutomationWrapper.Select(folderNode);

                    solution.App.Dte.ExecuteCommand("Edit.Paste");

                    var movedLinkedFileOpen = solution.WaitForItem("LinkedFiles", "MoveToFolder", "MovedLinkedFileOpen" + projectType.CodeExtension);
                    Assert.IsNotNull(movedLinkedFileOpen, "movedLinkedFileOpen");

                    Assert.IsTrue(File.Exists(Path.Combine(solution.Directory, Path.Combine(solution.Directory, "MovedLinkedFileOpen" + projectType.CodeExtension))));
                    Assert.IsFalse(File.Exists(Path.Combine(solution.Directory, "MoveToFolder\\MovedLinkedFileOpen" + projectType.CodeExtension)));

                    // window sholudn't have changed.
                    Assert.AreEqual(solution.App.Dte.Windows.Item("MovedLinkedFileOpen" + projectType.CodeExtension), openWindow);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveLinkedNodeOpenEdited() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {

                    var openWindow = solution.Project.ProjectItems.Item("MovedLinkedFileOpenEdit" + projectType.CodeExtension).Open();
                    Assert.IsNotNull(openWindow, "openWindow");

                    var selection = ((TextSelection)openWindow.Selection);
                    selection.SelectAll();
                    selection.Delete();

                    var projectNode = solution.FindItem("LinkedFiles", "MovedLinkedFileOpenEdit" + projectType.CodeExtension);

                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    solution.App.Dte.ExecuteCommand("Edit.Cut");

                    var folderNode = solution.FindItem("LinkedFiles", "MoveToFolder");
                    AutomationWrapper.Select(folderNode);

                    solution.App.Dte.ExecuteCommand("Edit.Paste");

                    var movedLinkedFileOpenEdit = solution.WaitForItem("LinkedFiles", "MoveToFolder", "MovedLinkedFileOpenEdit" + projectType.CodeExtension);
                    Assert.IsNotNull(movedLinkedFileOpenEdit, "movedLinkedFileOpenEdit");

                    Assert.IsTrue(File.Exists(Path.Combine(solution.Directory, "MovedLinkedFileOpenEdit" + projectType.CodeExtension)));
                    Assert.IsFalse(File.Exists(Path.Combine(solution.Directory, "MoveToFolder\\MovedLinkedFileOpenEdit" + projectType.CodeExtension)));

                    // window sholudn't have changed.
                    Assert.AreEqual(solution.App.Dte.Windows.Item("MovedLinkedFileOpenEdit" + projectType.CodeExtension), openWindow);

                    Assert.AreEqual(openWindow.Document.Saved, false);
                    openWindow.Document.Save();

                    Assert.AreEqual(new FileInfo(Path.Combine(solution.Directory, "MovedLinkedFileOpenEdit" + projectType.CodeExtension)).Length, (long)0);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveLinkedNodeFileExistsButNotInProject() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {

                    var fileNode = solution.FindItem("LinkedFiles", "FileNotInProject" + projectType.CodeExtension);
                    Assert.IsNotNull(fileNode, "projectNode");
                    AutomationWrapper.Select(fileNode);

                    solution.App.Dte.ExecuteCommand("Edit.Cut");

                    var folderNode = solution.FindItem("LinkedFiles", "FolderWithAFile");
                    AutomationWrapper.Select(folderNode);

                    solution.App.Dte.ExecuteCommand("Edit.Paste");

                    // item should have moved
                    var fileNotInProject = solution.WaitForItem("LinkedFiles", "FolderWithAFile", "FileNotInProject" + projectType.CodeExtension);
                    Assert.IsNotNull(fileNotInProject, "fileNotInProject");

                    // but it should be the linked file on disk outside of our project, not the file that exists on disk at the same location.
                    var autoItem = solution.Project.ProjectItems.Item("FolderWithAFile").Collection.Item("FileNotInProject" + projectType.CodeExtension);
                    Assert.AreEqual(Path.Combine(solution.Directory, "FileNotInProject" + projectType.CodeExtension), autoItem.Properties.Item("FullPath").Value);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DeleteLinkedNode() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {
                    var projectNode = solution.FindItem("LinkedFiles", "DeletedLinkedFile" + projectType.CodeExtension);
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    solution.App.Dte.ExecuteCommand("Edit.Delete");

                    projectNode = solution.FindItem("LinkedFiles", "DeletedLinkedFile" + projectType.CodeExtension);
                    Assert.AreEqual(null, projectNode);
                    Assert.IsTrue(File.Exists(Path.Combine(solution.Directory, "DeletedLinkedFile" + projectType.CodeExtension)));
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void LinkedFileInProjectIgnored() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {
                    var projectNode = solution.FindItem("LinkedFiles", "Foo", "LinkedInModule" + projectType.CodeExtension);

                    Assert.IsNull(projectNode);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SaveAsCreateLink() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {

                    var autoItem = solution.Project.ProjectItems.Item("SaveAsCreateLink" + projectType.CodeExtension);
                    var isLinkFile = autoItem.Properties.Item("IsLinkFile").Value;
                    Assert.AreEqual(isLinkFile, false);

                    var itemWindow = autoItem.Open();

                    autoItem.SaveAs("..\\SaveAsCreateLink" + projectType.CodeExtension);


                    autoItem = solution.Project.ProjectItems.Item("SaveAsCreateLink" + projectType.CodeExtension);
                    isLinkFile = autoItem.Properties.Item("IsLinkFile").Value;
                    Assert.AreEqual(isLinkFile, true);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SaveAsCreateFile() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {

                    var autoItem = solution.Project.ProjectItems.Item("SaveAsCreateFile" + projectType.CodeExtension);
                    var isLinkFile = autoItem.Properties.Item("IsLinkFile").Value;
                    Assert.AreEqual(isLinkFile, true);

                    var itemWindow = autoItem.Open();

                    autoItem.SaveAs(Path.Combine(solution.Directory, "LinkedFiles\\SaveAsCreateFile" + projectType.CodeExtension));

                    autoItem = solution.Project.ProjectItems.Item("SaveAsCreateFile" + projectType.CodeExtension);
                    isLinkFile = autoItem.Properties.Item("IsLinkFile").Value;
                    Assert.AreEqual(isLinkFile, false);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SaveAsCreateFileNewDirectory() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {

                    var autoItem = solution.Project.ProjectItems.Item("SaveAsCreateFileNewDirectory" + projectType.CodeExtension);
                    var isLinkFile = autoItem.Properties.Item("IsLinkFile").Value;
                    Assert.AreEqual(isLinkFile, true);

                    var itemWindow = autoItem.Open();

                    Directory.CreateDirectory(Path.Combine(solution.Directory, "LinkedFiles\\CreatedDirectory"));
                    autoItem.SaveAs(Path.Combine(solution.Directory, "LinkedFiles\\CreatedDirectory\\SaveAsCreateFileNewDirectory" + projectType.CodeExtension));


                    autoItem = solution.Project.ProjectItems.Item("CreatedDirectory").Collection.Item("SaveAsCreateFileNewDirectory" + projectType.CodeExtension);
                    isLinkFile = autoItem.Properties.Item("IsLinkFile").Value;
                    Assert.AreEqual(isLinkFile, false);
                }
            }
        }

        /// <summary>
        /// Adding a duplicate link to the same item
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItem() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {
                    var projectNode = solution.FindItem("LinkedFiles", "FolderWithAFile");
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    var addExistingDlg = new AddExistingItemDialog(solution.App.OpenDialogWithDteExecuteCommand("Project.AddExistingItem"));
                    addExistingDlg.FileName = Path.Combine(solution.Directory, "ExistingItem" + projectType.CodeExtension);
                    addExistingDlg.AddLink();

                    var existingItem = solution.WaitForItem("LinkedFiles", "FolderWithAFile", "ExistingItem" + projectType.CodeExtension);
                    Assert.IsNotNull(existingItem, "existingItem");
                }
            }
        }

        /// <summary>
        /// Adding a link to a folder which is already linked in somewhere else.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItemAndItemIsAlreadyLinked() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {
                    var projectNode = solution.FindItem("LinkedFiles", "AlreadyLinkedFolder");
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    var addExistingDlg = new AddExistingItemDialog(solution.App.OpenDialogWithDteExecuteCommand("Project.AddExistingItem"));
                    addExistingDlg.FileName = Path.Combine(solution.Directory, "FileNotInProject" + projectType.CodeExtension);
                    addExistingDlg.AddLink();

                    solution.App.WaitForDialog();
                    VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "There is already a link to", "A project cannot have more than one link to the same file.", "FileNotInProject.py");
                }
            }
        }

        /// <summary>
        /// Adding a duplicate link to the same item.
        /// 
        /// Also because the linked file dir is "LinkedFilesDir" which is a substring of "LinkedFiles" (our project name)
        /// this verifies we deal with the project name string comparison correctly (including a \ at the end of the
        /// path).
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItemAndLinkAlreadyExists() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {
                    var projectNode = solution.FindItem("LinkedFiles", "Bar");
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    var addExistingDlg = new AddExistingItemDialog(solution.App.OpenDialogWithDteExecuteCommand("Project.AddExistingItem"));
                    addExistingDlg.FileName = Path.Combine(solution.Directory, "LinkedFilesDir\\SomeLinkedFile" + projectType.CodeExtension);
                    addExistingDlg.AddLink();

                    solution.App.WaitForDialog();
                    VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "There is already a link to", "SomeLinkedFile" + projectType.CodeExtension);
                }
            }
        }

        /// <summary>
        /// Adding new linked item when file of same name exists (when the file only exists on disk)
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItemAndFileByNameExistsOnDiskButNotInProject() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {
                    var projectNode = solution.FindItem("LinkedFiles", "FolderWithAFile");
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);


                    var addExistingDlg = new AddExistingItemDialog(solution.App.OpenDialogWithDteExecuteCommand("Project.AddExistingItem"));
                    addExistingDlg.FileName = Path.Combine(solution.Directory, "ExistsOnDiskButNotInProject" + projectType.CodeExtension);
                    addExistingDlg.AddLink();

                    solution.App.WaitForDialog();
                    VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "There is already a file of the same name in this folder.");
                }
            }
        }

        /// <summary>
        /// Adding new linked item when file of same name exists (both in the project and on disk)
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItemAndFileByNameExistsOnDiskAndInProject() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {
                    var projectNode = solution.FindItem("LinkedFiles", "FolderWithAFile");
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);


                    var addExistingDlg = new AddExistingItemDialog(solution.App.OpenDialogWithDteExecuteCommand("Project.AddExistingItem"));
                    addExistingDlg.FileName = Path.Combine(solution.Directory, "ExistsOnDiskAndInProject" + projectType.CodeExtension);
                    addExistingDlg.AddLink();

                    solution.App.WaitForDialog();
                    VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "There is already a file of the same name in this folder.");
                }
            }
        }

        /// <summary>
        /// Adding new linked item when file of same name exists (in the project, but not on disk)
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItemAndFileByNameExistsInProjectButNotOnDisk() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {
                    var projectNode = solution.FindItem("LinkedFiles", "FolderWithAFile");
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    var addExistingDlg = new AddExistingItemDialog(solution.App.OpenDialogWithDteExecuteCommand("Project.AddExistingItem"));
                    addExistingDlg.FileName = Path.Combine(solution.Directory, "ExistsInProjectButNotOnDisk" + projectType.CodeExtension);
                    addExistingDlg.AddLink();

                    solution.App.WaitForDialog();
                    VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "There is already a file of the same name in this folder.");
                }
            }
        }

        /// <summary>
        /// Adding new linked item when the file lives in the project dir but not in the directory we selected
        /// Add Existing Item from.  We should add the file to the directory where it lives.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItemAsLinkButFileExistsInProjectDirectory() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {
                    var projectNode = solution.FindItem("LinkedFiles", "Foo");
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    var addExistingDlg = new AddExistingItemDialog(solution.App.OpenDialogWithDteExecuteCommand("Project.AddExistingItem"));
                    addExistingDlg.FileName = Path.Combine(solution.Directory, "LinkedFiles\\Foo\\AddExistingInProjectDirButNotInProject" + projectType.CodeExtension);
                    addExistingDlg.AddLink();

                    var addExistingInProjectDirButNotInProject = solution.WaitForItem("LinkedFiles", "Foo", "AddExistingInProjectDirButNotInProject" + projectType.CodeExtension);
                    Assert.IsNotNull(addExistingInProjectDirButNotInProject, "addExistingInProjectDirButNotInProject");
                }
            }
        }

        /// <summary>
        /// Reaming the file name in the Link attribute is ignored.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RenamedLinkedFile() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {
                    var projectNode = solution.FindItem("LinkedFiles", "Foo", "NewNameForLinkFile" + projectType.CodeExtension);
                    Assert.IsNull(projectNode);

                    var renamedLinkFile = solution.FindItem("LinkedFiles", "Foo", "RenamedLinkFile" + projectType.CodeExtension);
                    Assert.IsNotNull(renamedLinkFile, "renamedLinkFile");
                }
            }
        }

        /// <summary>
        /// A link path outside of our project dir will result in the link being ignored.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void BadLinkPath() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {
                    var projectNode = solution.FindItem("LinkedFiles", "..");
                    Assert.IsNull(projectNode);

                    projectNode = solution.FindItem("LinkedFiles", "BadLinkPathFolder");
                    Assert.IsNull(projectNode);
                }
            }
        }

        /// <summary>
        /// A rooted link path is ignored.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RootedLinkIgnored() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {
                    var projectNode = solution.FindItem("LinkedFiles", "RootedLinkIgnored" + projectType.CodeExtension);
                    Assert.IsNull(projectNode);
                }
            }
        }

        /// <summary>
        /// A rooted link path is ignored.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RootedIncludeIgnored() {
            foreach (var projectType in ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs()) {
                    var rootedIncludeIgnored = solution.FindItem("LinkedFiles", "RootedIncludeIgnored" + projectType.CodeExtension);
                    Assert.IsNotNull(rootedIncludeIgnored, "rootedIncludeIgnored");
                }
            }
        }
    }
}