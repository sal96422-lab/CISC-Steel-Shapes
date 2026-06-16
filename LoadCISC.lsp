; Auto-loads the CISC Metric Sections .NET plugin on AutoCAD startup
(defun s::startup ()
  (command "NETLOAD"
    (strcat (getenv "APPDATA")
            "\\Autodesk\\ApplicationPlugins\\CISCSections.bundle\\Contents\\CISCSections.dll")
  )
)
