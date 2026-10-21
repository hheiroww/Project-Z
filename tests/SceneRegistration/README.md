# Queued descendants and short XAML text

This generic package consumer exercises first-frame child/grandchild registration,
duplicate suppression, detached/removed parents and reattachment. It uses real
vector-font ink/line metrics to check short labels, clipping, multiple lines and
padding, and invokes the empty-text render path to verify that font-mode refresh
preserves the arranged box. It creates no Game platform, window or graphics device.

After restoring the candidate package from a local feed, run:

```powershell
dotnet run --project tests/SceneRegistration/SceneRegistration.csproj -c Release -p:RestoreAdditionalProjectSources=<absolute-package-directory>
```

`ProjectZRegressionVersion` defaults to 2026.10.20.2. Overriding it with 2026.10.20.1
provides the published regression oracle: queued-parent descendant registration
fails before the correction. Real rendered icons/nodes/text require a separate
native consumer check; these CPU probes do not claim pixel or graphics acceptance.
