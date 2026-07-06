using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Modules;
using Blish_HUD.Modules.Managers;
using Blish_HUD.Settings;

namespace Frtal.ModuleName {

    [Export(typeof(Blish_HUD.Modules.Module))]
    public class ModuleNameModule : Blish_HUD.Modules.Module {

        private static readonly Logger Logger =
            Logger.GetLogger<ModuleNameModule>();

        private SettingEntry<bool> _exampleSetting;
        private CornerIcon _cornerIcon;

        [ImportingConstructor]
        public ModuleNameModule(
            [Import("ModuleParameters")] ModuleParameters moduleParameters)
            : base(moduleParameters) { }

        protected override void DefineSettings(SettingCollection settings) {
            _exampleSetting = settings.DefineSetting(
                "ExampleSetting", true,
                () => "Display name",
                () => "Description shown in the settings panel.");
        }

        protected override async Task LoadAsync() {
            // Heavy init here (services, catalogs). Runs off the game thread.
            await Task.CompletedTask;
        }

        protected override void OnModuleLoaded(EventArgs e) {
            // UI creation here (game thread). Icons must be monochrome white
            // silhouettes on transparency — Blish tints them itself.
            _cornerIcon = new CornerIcon(
                ModuleParameters.ContentsManager.GetTexture("ref/icon.png"),
                ModuleParameters.ContentsManager.GetTexture("ref/icon_hover.png"),
                "Module Name");
            base.OnModuleLoaded(e);
        }

        protected override void Update(Microsoft.Xna.Framework.GameTime gameTime) {
            // The ONLY place UI state is mutated. Background threads set
            // volatile flags; this method applies them.
        }

        protected override void Unload() {
            // Dispose EVERYTHING created above; detach every event handler.
            _cornerIcon?.Dispose();
        }
    }
}
