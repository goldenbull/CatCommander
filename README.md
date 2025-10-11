# CatCommander
a cross-platform mouse-free file manager written in Avalonia, tributing to Total Commander

# TODO

- Compressed Files
  - view
  - compress
  - decompress
- File preview
  - text, markdown
  - office
  - image
  - hex
  - in the other panel or open a new preview window
- open cmd/powershell/bash/zsh in current folder
- compare folders
- filter file types (ctrl+F12/11/10)
- navigation back and forth
- goto arbitrary folder (ctrl shift c)
- file operation in background thread
- tabs operation
  - create new
  - duplicate to the other panel
  - favorites and common folders
- sort in different ways (ctrl+f3/f4)
- fast filter by filename
- SFTP
- FTP
- customized theme

# design

The overall architecture is quite simple, just a typical MVVM app. There three main parts:
1. data source: read files info from file system, compressed archive, sftp and ftp, etc.
2. UI
3. keymap management, combining key events and commands 