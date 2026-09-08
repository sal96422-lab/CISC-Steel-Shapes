; Auto-loads the CISC Metric Sections .NET plugin on AutoCAD startup.
(defun s::startup ( / dllPath)
  (setq dllPath
    (strcat (getenv "APPDATA")
            "\\Autodesk\\ApplicationPlugins\\CISCSections.bundle\\Contents\\CISCSections.dll")
  )
  (if (findfile dllPath)
    (progn
      (command "_.NETLOAD" dllPath)
      (princ "\nCISC Metric Sections startup loader ran. Type CISCINSERT or check the CISC Sections ribbon tab.")
    )
    (princ "\nCISC Metric Sections startup loader could not find CISCSections.dll.")
  )
  (princ)
)
