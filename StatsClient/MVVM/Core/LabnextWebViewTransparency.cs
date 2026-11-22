using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows.Media;

namespace StatsClient.MVVM.Core;

/// <summary>
/// In dark scheme only: transparent WebView2 host + injected <see cref="HostStylesheetFileName"/>.
/// Light scheme: native Labnext styling, opaque white WebView background.
/// </summary>
public static class LabnextWebViewTransparency
{
    public const string HostStylesheetFileName = "LabnextHost.css";

    private const string EmbeddedResourceName = "StatsClient.Themes.LabnextHost.css";
    private const string LogoEmbeddedResourceName = "StatsClient.Images.Other.Labnext_Logo_RGB.svg";
    private const string BootstrapScript = """
(function() {
  if (window.__statsClientLabnextHostBootstrapped) return;
  window.__statsClientLabnextHostBootstrapped = true;
  window.__statsClientLabnextHostStyleEnabled = false;

  const STYLE_ID = 'stats-client-labnext-host-css';
  const TILE_BG = 'rgba(45, 58, 75, 0.96)';
  const TILE_TEXT = '#E2E8F0';
  const NAV_BG = '#53667B';
  const DARK_TEXT = new Set([
    'rgb(0, 0, 0)', 'rgb(51, 51, 51)', 'rgb(33, 37, 41)', 'rgb(34, 34, 34)',
    'rgb(68, 68, 68)', 'rgb(85, 85, 85)', 'black'
  ]);

  function parseRgb(bg) {
    const m = bg && bg.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)/);
    return m ? [+m[1], +m[2], +m[3]] : null;
  }

  function luminance(rgb) {
    return 0.299 * rgb[0] + 0.587 * rgb[1] + 0.114 * rgb[2];
  }

  function isLightSurface(bg) {
    const rgb = parseRgb(bg);
    if (!rgb) return false;
    const lum = luminance(rgb);
    return lum > 160 || (lum > 125 && rgb[2] >= rgb[0]);
  }

  function isPageCanvasBackground(bg) {
    const rgb = parseRgb(bg);
    if (!rgb) return false;
    const lum = luminance(rgb);
    if (lum >= 200 && rgb[0] >= 235 && rgb[1] >= 235 && rgb[2] >= 235) return true;
    if (lum >= 175 && lum < 200 && Math.min(rgb[0], rgb[1], rgb[2]) >= 210) return true;
    if (lum > 175) return false;
    return rgb[2] >= rgb[0] && rgb[2] >= rgb[1] - 15 && lum >= 55;
  }

  function isLabnextNavy(bg) {
    const rgb = parseRgb(bg);
    if (!rgb) return false;
    const lum = luminance(rgb);
    return lum < 130 && rgb[2] > rgb[0] + 15 && rgb[0] <= 90;
  }

  function isChrome(el) {
    return !!el.closest('header, nav, .navbar, [class*="navbar"], [class*="top-nav"], [class*="main-nav"], [class*="widget-header"], .box-header, .panel-heading');
  }

  function isInsideWidget(el) {
    return !!el.closest('[class*="widget"], [class*="Widget"], .panel, .box, [class*="glance"], [class*="Glance"], [class*="alert"], [class*="Alert"], [class*="activity"], [class*="Activity"]');
  }

  function isTile(el) {
    const cls = (el.className || '').toString().toLowerCase();
    if (/widget|glance|stat|tile|info-box|small-box|panel-body|metric/.test(cls)) return true;
    const rect = el.getBoundingClientRect();
    return rect.width > 0 && rect.width < 420 && rect.height > 40 && rect.height < 320;
  }

  window.__statsClientLabnextInjectCss = function(cssText) {
    let style = document.getElementById(STYLE_ID);
    if (!style) {
      style = document.createElement('style');
      style.id = STYLE_ID;
      style.type = 'text/css';
      (document.head || document.documentElement).appendChild(style);
    }
    style.textContent = cssText || '';
    document.documentElement.classList.add('stats-client-labnext-dark');
    if (document.body) document.body.classList.add('stats-client-labnext-dark');
  };

  window.__statsClientLabnextDetachCss = function() {
    document.getElementById(STYLE_ID)?.remove();
    document.documentElement.classList.remove('stats-client-labnext-dark');
    if (document.body) document.body.classList.remove('stats-client-labnext-dark');
  };

  const LABNEXT_LOGO_ASSET = 'labnext_logo_rgb.svg';

  function referencesLabnextRgbLogo(el) {
    const blob = (
      (el.getAttribute('src') || '') +
      (el.getAttribute('data') || '') +
      (el.getAttribute('href') || '') +
      (el.getAttribute('xlink:href') || '')
    ).toLowerCase();
    return blob.includes(LABNEXT_LOGO_ASSET);
  }

  window.__statsClientLabnextReplaceLogo = function() {
    const svgMarkup = window.__statsClientLabnextLogoSvg;
    if (!svgMarkup) return;

    document.querySelectorAll('img, object, embed, image, source, use, a').forEach(el => {
      if (!(el instanceof HTMLElement)) return;
      if (el.classList.contains('stats-labnext-logo-host')) return;
      if (!referencesLabnextRgbLogo(el)) return;
      if (el.dataset.statsLogoReplaced) return;

      const wrap = document.createElement('span');
      wrap.className = 'stats-labnext-logo-host';
      wrap.innerHTML = svgMarkup;
      const svg = wrap.querySelector('svg');
      if (svg) {
        svg.style.height = '36px';
        svg.style.width = 'auto';
        svg.style.display = 'block';
        svg.style.maxWidth = '180px';
      }
      el.replaceWith(wrap);
      wrap.dataset.statsLogoReplaced = '1';
    });
  };

  window.__statsClientLabnextClearCanvas = function() {
    if (!window.__statsClientLabnextHostStyleEnabled) return;
    document.querySelectorAll('html, body, div, section, article, main, aside, #content, #main, [class*="dashboard"], [class*="workspace"], [class*="content"], .row, [class*="row"]').forEach(el => {
      if (!(el instanceof HTMLElement) || isChrome(el) || isInsideWidget(el)) return;
      const bg = getComputedStyle(el).backgroundColor;
      if (!isPageCanvasBackground(bg)) return;
      const rect = el.getBoundingClientRect();
      if (rect.width < 80 || rect.height < 40) return;
      el.style.setProperty('background-color', 'transparent', 'important');
      el.style.setProperty('background-image', 'none', 'important');
    });
  };

  function isLogoChrome(el) {
    return !!el.closest('.stats-labnext-logo-host, .navbar-brand, .navbar-header, [class*="logo"], [class*="Logo"], [class*="brand"], [class*="Brand"]')
      || !!el.querySelector?.('.stats-labnext-logo-host');
  }

  function isMenuBar(el) {
    const cls = (el.className || '').toString().toLowerCase();
    const id = (el.id || '').toLowerCase();
    return id === 'global_nav'
      || el.matches('#global_nav, .menu.navbar, nav.menu, .navbar, .navbar-default, .navbar-collapse, .navbar-nav, .navbar-nav > li')
      || /main-nav|nav-bar|navbar/.test(cls);
  }

  function isTopUtilityBar(el) {
    const cls = (el.className || '').toString().toLowerCase();
    const id = (el.id || '').toLowerCase();
    return id === 'utility_nav'
      || /utility|top-link|account-nav|user-nav|header-link/.test(cls)
      || !!el.closest('#utility_nav, [class*="utility"], header .pull-right, .navbar-right:not(.navbar-collapse)');
  }

  function isWidgetTitleBar(el) {
    const cls = (el.className || '').toString().toLowerCase();
    return /widget-header|box-header|panel-heading|card-header/.test(cls)
      || !!el.closest('[class*="widget-header"], .box-header, .panel-heading, .card-header');
  }

  function isLabDefaultLogo(el) {
    const src = (el.getAttribute('src') || '').toLowerCase();
    const alt = (el.getAttribute('alt') || '').toLowerCase();
    return src.includes('/lab/default/logo') || alt.includes('ceramics dental');
  }

  window.__statsClientLabnextHideLabLogo = function() {
    if (!window.__statsClientLabnextHostStyleEnabled) return;
    document.querySelectorAll('img').forEach(el => {
      if (!(el instanceof HTMLElement) || !isLabDefaultLogo(el)) return;
      el.style.setProperty('display', 'none', 'important');
      el.style.setProperty('visibility', 'hidden', 'important');
      el.style.setProperty('width', '0', 'important');
      el.style.setProperty('height', '0', 'important');
    });
  };

  window.__statsClientLabnextClearTopUtilityChrome = function() {
    if (!window.__statsClientLabnextHostStyleEnabled) return;
    document.querySelectorAll('#utility_nav, [class*="utility"], header .pull-right, header .navbar-right, [class*="top-link"], [class*="account-nav"]').forEach(el => {
      if (!(el instanceof HTMLElement)) return;
      el.style.setProperty('background-color', 'transparent', 'important');
      el.style.setProperty('background-image', 'none', 'important');
      el.style.setProperty('border-color', 'transparent', 'important');
    });
  };

  window.__statsClientLabnextPaintChrome = function() {
    if (!window.__statsClientLabnextHostStyleEnabled) return;
    document.querySelectorAll('#global_nav, .menu.navbar, nav.menu, nav, .navbar, .navbar-default, .navbar-collapse, .navbar-nav, .navbar-nav > li, [class*="main-nav"], [class*="nav-bar"], [class*="widget-header"], .box-header, .panel-heading, .card-header, div, section').forEach(el => {
      if (!(el instanceof HTMLElement) || isLogoChrome(el) || isTopUtilityBar(el)) return;
      const paintNav = isMenuBar(el);
      const paintTitle = isWidgetTitleBar(el);
      const paintNavy = isLabnextNavy(getComputedStyle(el).backgroundColor);
      if (!paintNav && !paintTitle && !paintNavy) return;
      el.style.setProperty('background-color', NAV_BG, 'important');
      el.style.setProperty('background-image', 'none', 'important');
      if (DARK_TEXT.has(getComputedStyle(el).color)) {
        el.style.setProperty('color', TILE_TEXT, 'important');
      }
    });
  };

  window.__statsClientLabnextClearLogoChrome = function() {
    if (!window.__statsClientLabnextHostStyleEnabled) return;
    document.querySelectorAll('.stats-labnext-logo-host, .navbar-brand, .navbar-header, [class*="logo"], [class*="brand"]').forEach(el => {
      if (!(el instanceof HTMLElement)) return;
      el.style.setProperty('background-color', 'transparent', 'important');
      el.style.setProperty('background-image', 'none', 'important');
      el.style.setProperty('box-shadow', 'none', 'important');
    });
    document.querySelectorAll('.stats-labnext-logo-host').forEach(host => {
      let parent = host.parentElement;
      for (let depth = 0; depth < 4 && parent; depth++) {
        if (parent.matches('nav, .navbar, .navbar-nav, .navbar-collapse')) break;
        parent.style.setProperty('background-color', 'transparent', 'important');
        parent.style.setProperty('background-image', 'none', 'important');
        parent = parent.parentElement;
      }
    });
  };

  window.__statsClientLabnextPaintTiles = function() {
    if (!window.__statsClientLabnextHostStyleEnabled) return;
    document.querySelectorAll('div, section, article, td, th, li, table, ul').forEach(el => {
      if (!(el instanceof HTMLElement)) return;
      if (el.closest('.stats-labnext-logo-host')) return;
      const bg = getComputedStyle(el).backgroundColor;
      if (!isLightSurface(bg)) return;
      const inWidget = isInsideWidget(el);
      if (!inWidget && !isTile(el) && el.getBoundingClientRect().width > 500) return;
      el.style.setProperty('background-color', TILE_BG, 'important');
      if (DARK_TEXT.has(getComputedStyle(el).color)) {
        el.style.setProperty('color', TILE_TEXT, 'important');
      }
    });
  };

  window.__statsClientLabnextPaintSurfaces = function() {
    window.__statsClientLabnextClearCanvas?.();
    window.__statsClientLabnextPaintChrome?.();
    window.__statsClientLabnextClearTopUtilityChrome?.();
    window.__statsClientLabnextClearLogoChrome?.();
    window.__statsClientLabnextHideLabLogo?.();
    window.__statsClientLabnextPaintTiles?.();
    window.__statsClientLabnextReplaceLogo?.();
    window.__statsClientLabnextClearLogoChrome?.();
    window.__statsClientLabnextClearTopUtilityChrome?.();
    window.__statsClientLabnextHideLabLogo?.();
  };

  window.__statsClientLabnextApplyHostStyle = function(cssText, enabled) {
    window.__statsClientLabnextHostStyleEnabled = !!enabled;
    if (!enabled) {
      window.__statsClientLabnextDetachCss();
      return Promise.resolve(true);
    }
    window.__statsClientLabnextInjectCss(cssText);
    window.__statsClientLabnextPaintSurfaces();
    return new Promise(resolve => {
      requestAnimationFrame(() => {
        window.__statsClientLabnextPaintSurfaces();
        setTimeout(() => {
          window.__statsClientLabnextPaintSurfaces();
          setTimeout(() => {
            window.__statsClientLabnextPaintSurfaces();
            const ready = !enabled || (
              !!document.getElementById('stats-client-labnext-host-css') &&
              document.documentElement.classList.contains('stats-client-labnext-dark')
            );
            resolve(ready);
          }, 400);
        }, 200);
      });
    });
  };
})();
""";

    public static bool IsHostStyleEnabled => ColorSchemeManager.IsDarkScheme;

    public static void ConfigureHost(WebView2 webView)
    {
        webView.DefaultBackgroundColor = IsHostStyleEnabled
            ? System.Drawing.Color.Transparent
            : System.Drawing.Color.White;
    }

    public static async Task RegisterAsync(CoreWebView2 coreWebView2)
    {
        WriteHostStylesheetToDisk();
        await coreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BootstrapScript);
    }

    public static async Task<bool> ApplyForCurrentSchemeAsync(WebView2 webView, CoreWebView2? coreWebView2)
    {
        ConfigureHost(webView);

        if (coreWebView2 is null)
            return false;

        string css = LoadEmbeddedStylesheet();
        string logoSvg = LoadHostLogoSvg();
        string enabled = IsHostStyleEnabled ? "true" : "false";
        string cssJson = JsonSerializer.Serialize(css);
        string logoJson = JsonSerializer.Serialize(logoSvg);
        string script =
            "(async function() {" +
            "window.__statsClientLabnextLogoSvg=" + logoJson + ";" +
            "if (!window.__statsClientLabnextApplyHostStyle) return false;" +
            "return await window.__statsClientLabnextApplyHostStyle(" + cssJson + ", " + enabled + ");" +
            "})();";

        string raw = await coreWebView2.ExecuteScriptAsync(script);
        return ParseScriptBool(raw);
    }

    private static bool ParseScriptBool(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        string trimmed = json.Trim().Trim('"');
        return bool.TryParse(trimmed, out bool value) && value;
    }

    public static Brush GetHostPanelBackgroundBrush()
    {
        if (IsHostStyleEnabled)
            return Brushes.Transparent;

        if (ColorSchemeResourceCatalog.TryGetBrush("WhiteBackground", out var lightBrush))
            return lightBrush;

        return Brushes.White;
    }

    private static void WriteHostStylesheetToDisk()
    {
        string themesDir = Path.Combine(AppContext.BaseDirectory, "Themes");
        Directory.CreateDirectory(themesDir);
        File.WriteAllText(Path.Combine(themesDir, HostStylesheetFileName), LoadEmbeddedStylesheet(), Encoding.UTF8);
    }

    private static string LoadEmbeddedStylesheet()
        => LoadEmbeddedText(EmbeddedResourceName) ?? "html,body{background:transparent!important;}";

    private static string LoadHostLogoSvg()
    {
        string? raw = LoadEmbeddedText(LogoEmbeddedResourceName);
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        return PrepareSvgForHtmlInjection(raw);
    }

    private static string PrepareSvgForHtmlInjection(string svg)
    {
        svg = svg.Trim();
        if (svg.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
        {
            int end = svg.IndexOf("?>", StringComparison.Ordinal);
            if (end >= 0)
                svg = svg[(end + 2)..].TrimStart();
        }

        return svg;
    }

    private static string? LoadEmbeddedText(string resourceName)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;

        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
