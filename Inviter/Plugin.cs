using Inviter.Installers;
using IPA;
using SiraUtil.Zenject;
using IPALogger = IPA.Logging.Logger;

namespace Inviter;

[NoEnableDisable, Plugin(RuntimeOptions.DynamicInit)]
public class Plugin
{
    [Init]
    public Plugin(IPALogger logger, Zenjector zenjector)
    {
        zenjector.UseLogger(logger);
        zenjector.Install<InviterCoreInstaller>(Location.App);
    }
}