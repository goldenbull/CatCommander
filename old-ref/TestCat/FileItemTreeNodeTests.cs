using CatCommander.Models;
using CatCommander.Utils;

namespace TestCat;

public class FileItemTreeNodeTests
{
    private class MockFileSystemItem : IFileSystemItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime Created { get; set; } = DateTime.MinValue;
        public DateTime Modified { get; set; } = DateTime.MinValue;
        public DateTime Accessed { get; set; } = DateTime.MinValue;
        public FileSystemItemType ItemType { get; set; } = FileSystemItemType.File;
        public bool CanRead { get; set; } = true;
        public bool CanWrite { get; set; } = true;
        public bool CanExecute { get; set; } = false;
        public bool IsHidden { get; set; } = false;
        public string? LinkTarget { get; set; }
        public string DisplaySize { get; set; } = string.Empty;
        public string DisplayIcon { get; set; } = string.Empty;
    }

    [Fact]
    public void CreateFrom_EmptyList_ReturnsRootWithNoChildren()
    {
        // Arrange
        var items = new List<IFileSystemItem>();

        // Act
        var root = FileItemTreeNode.CreateFrom(items);

        // Assert
        Assert.NotNull(root);
        Assert.Empty(root.Children);
        Assert.False(root.HasChildren);
        Assert.Equal(string.Empty, root.FullPath);
    }

    [Fact]
    public void CreateFrom_SingleFile_CreatesRootWithOneChild()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem
            {
                Name = "test.txt",
                FullPath = "test.txt",
                ItemType = FileSystemItemType.File
            }
        };

        // Act
        var root = FileItemTreeNode.CreateFrom(items);

        // Assert
        Assert.True(root.HasChildren);
        Assert.Single(root.Children);
        Assert.True(root.Children.ContainsKey("test.txt"));
        Assert.Equal("test.txt", root.Children["test.txt"].Name);
        Assert.False(root.Children["test.txt"].IsDirectory);
    }

    [Fact]
    public void CreateFrom_NestedStructure_CreatesCorrectHierarchy()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem
            {
                Name = "file1.txt",
                FullPath = "dir1/file1.txt",
                ItemType = FileSystemItemType.File
            },
            new MockFileSystemItem
            {
                Name = "file2.txt",
                FullPath = "dir1/subdir/file2.txt",
                ItemType = FileSystemItemType.File
            },
            new MockFileSystemItem
            {
                Name = "file3.txt",
                FullPath = "dir2/file3.txt",
                ItemType = FileSystemItemType.File
            }
        };

        // Act
        var root = FileItemTreeNode.CreateFrom(items);

        // Assert
        Assert.Equal(2, root.Children.Count);
        Assert.True(root.Children.ContainsKey("dir1"));
        Assert.True(root.Children.ContainsKey("dir2"));

        var dir1 = root.Children["dir1"];
        Assert.True(dir1.IsDirectory);
        Assert.Equal(2, dir1.Children.Count);
        Assert.True(dir1.Children.ContainsKey("file1.txt"));
        Assert.True(dir1.Children.ContainsKey("subdir"));

        var subdir = dir1.Children["subdir"];
        Assert.True(subdir.IsDirectory);
        Assert.Single(subdir.Children);
        Assert.True(subdir.Children.ContainsKey("file2.txt"));

        var dir2 = root.Children["dir2"];
        Assert.True(dir2.IsDirectory);
        Assert.Single(dir2.Children);
        Assert.True(dir2.Children.ContainsKey("file3.txt"));
    }

    [Fact]
    public void CreateFrom_WithBackslashes_NormalizesToForwardSlashes()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem
            {
                Name = "file.txt",
                FullPath = @"dir1\dir2\file.txt",
                ItemType = FileSystemItemType.File
            }
        };

        // Act
        var root = FileItemTreeNode.CreateFrom(items);

        // Assert
        Assert.True(root.Children.ContainsKey("dir1"));
        var dir1 = root.Children["dir1"];
        Assert.True(dir1.Children.ContainsKey("dir2"));
        var dir2 = dir1.Children["dir2"];
        Assert.True(dir2.Children.ContainsKey("file.txt"));
    }

    [Fact]
    public void CreateFrom_WithCustomSeparator_UsesCorrectSeparator()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem
            {
                Name = "file.txt",
                FullPath = @"dir1\dir2\file.txt",
                ItemType = FileSystemItemType.File
            }
        };

        // Act
        var root = FileItemTreeNode.CreateFrom(items, '\\');

        // Assert
        Assert.True(root.Children.ContainsKey("dir1"));
        var dir1 = root.Children["dir1"];
        Assert.True(dir1.Children.ContainsKey("dir2"));
    }

    [Fact]
    public void GetItemsAtPath_RootPath_ReturnsTopLevelItems()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem { Name = "file1.txt", FullPath = "file1.txt" },
            new MockFileSystemItem { Name = "file2.txt", FullPath = "file2.txt" }
        };
        var root = FileItemTreeNode.CreateFrom(items);

        // Act
        var result = root.GetItemsAtPath(string.Empty);

        // Assert
        Assert.Equal(2, result.Count());
    }

    [Fact]
    public void GetItemsAtPath_ValidPath_ReturnsItemsAtThatLevel()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem { Name = "file1.txt", FullPath = "dir1/file1.txt" },
            new MockFileSystemItem { Name = "file2.txt", FullPath = "dir1/file2.txt" },
            new MockFileSystemItem { Name = "file3.txt", FullPath = "dir2/file3.txt" }
        };
        var root = FileItemTreeNode.CreateFrom(items);

        // Act
        var result = root.GetItemsAtPath("dir1");

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(result, n => n.Name == "file1.txt");
        Assert.Contains(result, n => n.Name == "file2.txt");
    }

    [Fact]
    public void GetItemsAtPath_InvalidPath_ReturnsEmpty()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem { Name = "file1.txt", FullPath = "dir1/file1.txt" }
        };
        var root = FileItemTreeNode.CreateFrom(items);

        // Act
        var result = root.GetItemsAtPath("nonexistent");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void FindNode_RootPath_ReturnsSelf()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem { Name = "file.txt", FullPath = "file.txt" }
        };
        var root = FileItemTreeNode.CreateFrom(items);

        // Act
        var result = root.FindNode(string.Empty);

        // Assert
        Assert.NotNull(result);
        Assert.Same(root, result);
    }

    [Fact]
    public void FindNode_ValidPath_ReturnsCorrectNode()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem { Name = "file.txt", FullPath = "dir1/dir2/file.txt" }
        };
        var root = FileItemTreeNode.CreateFrom(items);

        // Act
        var result = root.FindNode("dir1/dir2");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("dir2", result.Name);
        Assert.True(result.Children.ContainsKey("file.txt"));
    }

    [Fact]
    public void FindNode_InvalidPath_ReturnsNull()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem { Name = "file.txt", FullPath = "dir1/file.txt" }
        };
        var root = FileItemTreeNode.CreateFrom(items);

        // Act
        var result = root.FindNode("dir1/nonexistent/path");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetAllDescendants_FlatStructure_ReturnsAllItems()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem { Name = "file1.txt", FullPath = "file1.txt" },
            new MockFileSystemItem { Name = "file2.txt", FullPath = "file2.txt" }
        };
        var root = FileItemTreeNode.CreateFrom(items);

        // Act
        var result = root.GetAllDescendants();

        // Assert
        Assert.Equal(2, result.Count());
    }

    [Fact]
    public void GetAllDescendants_NestedStructure_ReturnsAllNodesRecursively()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem { Name = "file1.txt", FullPath = "dir1/file1.txt" },
            new MockFileSystemItem { Name = "file2.txt", FullPath = "dir1/subdir/file2.txt" },
            new MockFileSystemItem { Name = "file3.txt", FullPath = "dir2/file3.txt" }
        };
        var root = FileItemTreeNode.CreateFrom(items);

        // Act
        var result = root.GetAllDescendants().ToList();

        // Assert
        // Should include: dir1, file1.txt, subdir, file2.txt, dir2, file3.txt
        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void GetAllFiles_MixedStructure_ReturnsOnlyFiles()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem
            {
                Name = "file1.txt",
                FullPath = "dir1/file1.txt",
                ItemType = FileSystemItemType.File
            },
            new MockFileSystemItem
            {
                Name = "file2.txt",
                FullPath = "dir1/subdir/file2.txt",
                ItemType = FileSystemItemType.File
            },
            new MockFileSystemItem
            {
                Name = "dir3",
                FullPath = "dir1/dir3",
                ItemType = FileSystemItemType.Directory
            }
        };
        var root = FileItemTreeNode.CreateFrom(items);

        // Act
        var result = root.GetAllFiles().ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, node => Assert.False(node.IsDirectory));
    }

    [Fact]
    public void GetAllDirectories_MixedStructure_ReturnsOnlyDirectories()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem
            {
                Name = "file1.txt",
                FullPath = "dir1/file1.txt",
                ItemType = FileSystemItemType.File
            },
            new MockFileSystemItem
            {
                Name = "file2.txt",
                FullPath = "dir1/subdir/file2.txt",
                ItemType = FileSystemItemType.File
            },
            new MockFileSystemItem
            {
                Name = "dir3",
                FullPath = "dir2/dir3",
                ItemType = FileSystemItemType.Directory
            }
        };
        var root = FileItemTreeNode.CreateFrom(items);

        // Act
        var result = root.GetAllDirectories().ToList();

        // Assert
        // Should include: dir1, subdir, dir2, dir3
        Assert.Equal(4, result.Count);
        Assert.All(result, node => Assert.True(node.IsDirectory));
    }

    [Fact]
    public void IsDirectory_VirtualDirectory_ReturnsTrue()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem
            {
                Name = "file.txt",
                FullPath = "dir1/file.txt",
                ItemType = FileSystemItemType.File
            }
        };
        var root = FileItemTreeNode.CreateFrom(items);

        // Act
        var dir1 = root.Children["dir1"];

        // Assert
        Assert.True(dir1.IsDirectory);
        Assert.Null(dir1.Item); // Virtual directory has no item
    }

    [Fact]
    public void IsDirectory_ActualDirectory_ReturnsTrue()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem
            {
                Name = "mydir",
                FullPath = "mydir",
                ItemType = FileSystemItemType.Directory
            }
        };
        var root = FileItemTreeNode.CreateFrom(items);

        // Act
        var dir = root.Children["mydir"];

        // Assert
        Assert.True(dir.IsDirectory);
        Assert.NotNull(dir.Item);
    }

    [Fact]
    public void IsDirectory_File_ReturnsFalse()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem
            {
                Name = "file.txt",
                FullPath = "file.txt",
                ItemType = FileSystemItemType.File
            }
        };
        var root = FileItemTreeNode.CreateFrom(items);

        // Act
        var file = root.Children["file.txt"];

        // Assert
        Assert.False(file.IsDirectory);
    }

    [Fact]
    public void Children_CaseInsensitive_FindsItemRegardlessOfCase()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem
            {
                Name = "MyFile.txt",
                FullPath = "MyFile.txt",
                ItemType = FileSystemItemType.File
            }
        };
        var root = FileItemTreeNode.CreateFrom(items);

        // Assert
        Assert.True(root.Children.ContainsKey("myfile.txt"));
        Assert.True(root.Children.ContainsKey("MyFile.txt"));
        Assert.True(root.Children.ContainsKey("MYFILE.TXT"));
    }

    [Fact]
    public void CreateFrom_DuplicatePaths_DoesNotCreateDuplicateNodes()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem
            {
                Name = "file.txt",
                FullPath = "dir1/file.txt",
                ItemType = FileSystemItemType.File
            },
            new MockFileSystemItem
            {
                Name = "file.txt",
                FullPath = "dir1/file.txt",
                ItemType = FileSystemItemType.File
            }
        };

        // Act
        var root = FileItemTreeNode.CreateFrom(items);

        // Assert
        var dir1 = root.Children["dir1"];
        Assert.Single(dir1.Children);
    }

    [Fact]
    public void FullPath_VirtualDirectory_HasCorrectPath()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem
            {
                Name = "file.txt",
                FullPath = "dir1/dir2/file.txt",
                ItemType = FileSystemItemType.File
            }
        };
        var root = FileItemTreeNode.CreateFrom(items);

        // Act
        var dir1 = root.Children["dir1"];
        var dir2 = dir1.Children["dir2"];

        // Assert
        Assert.Equal("dir1", dir1.FullPath);
        Assert.Equal("dir1/dir2", dir2.FullPath);
    }

    [Fact]
    public void Name_VirtualDirectory_HasCorrectName()
    {
        // Arrange
        var items = new List<IFileSystemItem>
        {
            new MockFileSystemItem
            {
                Name = "file.txt",
                FullPath = "parentdir/childdir/file.txt",
                ItemType = FileSystemItemType.File
            }
        };
        var root = FileItemTreeNode.CreateFrom(items);

        // Act
        var parent = root.Children["parentdir"];
        var child = parent.Children["childdir"];

        // Assert
        Assert.Equal("parentdir", parent.Name);
        Assert.Equal("childdir", child.Name);
    }
}
