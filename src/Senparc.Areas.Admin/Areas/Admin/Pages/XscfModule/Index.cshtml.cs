using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Senparc.CO2NET.Extensions;
using Senparc.Core.Models.DataBaseModel;
using Senparc.Scf.Core.Enums;
using Senparc.Scf.Core.Models;
using Senparc.Scf.Core.Models.DataBaseModel;
using Senparc.Scf.Service;
using Senparc.Scf.XscfBase;
using Senparc.Service;
using Microsoft.Extensions.DependencyInjection;

namespace Senparc.Areas.Admin.Areas.Admin.Pages
{
    public class XscfModuleIndexModel : BaseAdminPageModel
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly XscfModuleService _xscfModuleService;
        private readonly SysMenuService _sysMenuService;

        public XscfModuleIndexModel(IServiceProvider serviceProvider, XscfModuleService xscfModuleService, SysMenuService sysMenuService)
        {
            CurrentMenu = "XscfModule";

            this._serviceProvider = serviceProvider;
            this._xscfModuleService = xscfModuleService;
            this._sysMenuService = sysMenuService;
        }

        [BindProperty(SupportsGet = true)]
        public int PageIndex { get; set; } = 1;

        /// <summary>
        /// 数据库已存的XscfModules
        /// </summary>
        public PagedList<XscfModule> XscfModules { get; set; }
        public List<IXscfRegister> NewXscfRegisters { get; set; }

        private void LoadNewXscfRegisters(PagedList<XscfModule> xscfModules)
        {
            NewXscfRegisters = Senparc.Scf.XscfBase.Register.RegisterList.Where(z => !xscfModules.Exists(m => m.Uid == z.Uid && m.Version == z.Version)).ToList() ?? new List<IXscfRegister>();
        }

        public async Task OnGetAsync()
        {
            XscfModules = await _xscfModuleService.GetObjectListAsync(PageIndex, 10, _ => true, _ => _.AddTime, Scf.Core.Enums.OrderingType.Descending);
            LoadNewXscfRegisters(XscfModules);
        }

        /// <summary>
        /// 扫描新模块
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> OnGetScanAsync(string uid)
        {
            if (uid.IsNullOrEmpty())
            {
                throw new Exception("模块不存在！");
            }

            var xscfRegister = Senparc.Scf.XscfBase.Register.RegisterList.FirstOrDefault(z => z.Uid == z.Uid);
            if (xscfRegister == null)
            {
                throw new Exception("模块不存在！");
            }

            var xscfModule = await _xscfModuleService.GetObjectAsync(z => z.Uid == z.Uid && z.Version == xscfRegister.Version);
            if (xscfModule != null)
            {
                throw new Exception("相同版本模块已安装，无需重复安装！");
            }

            XscfModules = await _xscfModuleService.GetObjectListAsync(PageIndex, 10, _ => true, _ => _.AddTime, Scf.Core.Enums.OrderingType.Descending).ConfigureAwait(false);

            var dto = XscfModules.Select(z => new CreateOrUpdate_XscfModuleDto(z.Name, z.Uid, z.MenuName, z.Version, z.Description, z.UpdateLog, z.AllowRemove, z.State)).ToList();

            //进行模块扫描
            var result = await Senparc.Scf.XscfBase.Register.ScanAndInstall(dto, _serviceProvider, async (register, installOrUpdate) =>
              {
                  var sysMenuService = _serviceProvider.GetService<SysMenuService>();

                  var topMenu = await sysMenuService.GetObjectAsync(z => z.MenuName == "扩展模块").ConfigureAwait(false);
                  var currentMenu = await sysMenuService.GetObjectAsync(z => z.ParentId == topMenu.Id && z.MenuName == register.MenuName).ConfigureAwait(false);//TODO: menu 还需要加一个锁定Uid的扩展属性
                  SysMenuDto menuDto;

                  if (installOrUpdate == InstallOrUpdate.Update && currentMenu != null)
                  {
                      //更新菜单
                      menuDto = sysMenuService.Mapper.Map<SysMenuDto>(currentMenu);
                      menuDto.MenuName = register.MenuName;//更新菜单名称
                  }
                  else
                  {
                      //新建菜单
                      menuDto = new SysMenuDto(true, null, register.MenuName, topMenu.Id, $"/Admin/XscfModule/Start/?uid={register.Uid}", "fa fa-bars", 5, true, null);
                  }
                  await sysMenuService.CreateOrUpdateAsync(menuDto).ConfigureAwait(false);
              }).ConfigureAwait(false);

            base.SetMessager(Scf.Core.Enums.MessageType.info, result, true);

            //if (backpage=="Start")
            return RedirectToPage("Start", new { uid = uid });//始终到详情页
            //return RedirectToPage("Index");
        }
    }
}