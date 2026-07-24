using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewHtmlBuilder
{
    private const string HostScript = """
        (() => {
          'use strict';
          const viewport = document.querySelector('.preview-viewport');
          const image = document.querySelector('img');
          const bridge = window.chrome && window.chrome.webview;
          const bridgeToken = document.body.dataset.bridgeToken;
          let spaceHeld = false;
          let activePointerId = null;
          let startX = 0;
          let startY = 0;
          let startScrollLeft = 0;
          let startScrollTop = 0;

          const canPan = () =>
            viewport.scrollWidth > viewport.clientWidth ||
            viewport.scrollHeight > viewport.clientHeight;

          const refreshCursor = () => {
            viewport.classList.toggle('can-pan', canPan());
            viewport.classList.toggle('space-held', spaceHeld && canPan());
          };

          const stopPan = event => {
            if (activePointerId === null ||
                (event && event.pointerId !== activePointerId)) {
              return;
            }
            if (viewport.hasPointerCapture(activePointerId)) {
              viewport.releasePointerCapture(activePointerId);
            }
            activePointerId = null;
            viewport.classList.remove('panning');
            refreshCursor();
          };

          const handleWheel = event => {
            if (event.ctrlKey) {
              event.preventDefault();
              if (!bridge) {
                return;
              }
              const rect = viewport.getBoundingClientRect();
              const anchorX = Math.max(0, Math.min(viewport.clientWidth, event.clientX - rect.left));
              const anchorY = Math.max(0, Math.min(viewport.clientHeight, event.clientY - rect.top));
              const contentX = Math.max(0, Math.min(1,
                (viewport.scrollLeft + anchorX) / Math.max(1, viewport.scrollWidth)));
              const contentY = Math.max(0, Math.min(1,
                (viewport.scrollTop + anchorY) / Math.max(1, viewport.scrollHeight)));
              bridge.postMessage({
                type: 'zoom',
                token: bridgeToken,
                direction: event.deltaY < 0 ? 'in' : 'out',
                contentX,
                contentY,
                anchorX,
                anchorY,
                viewportWidth: viewport.clientWidth,
                viewportHeight: viewport.clientHeight
              });
              return;
            }

            if (event.shiftKey) {
              const horizontalDelta = event.deltaY !== 0 ? event.deltaY : event.deltaX;
              if (horizontalDelta !== 0) {
                event.preventDefault();
                viewport.scrollLeft += horizontalDelta;
              }
            }
          };

          // WebView2 must allow Ctrl+Wheel into the renderer before this handler can
          // replace native document zoom with artwork-only zoom.
          window.addEventListener(
            'wheel',
            handleWheel,
            { capture: true, passive: false });

          viewport.addEventListener('pointerdown', event => {
            const isMiddlePan = event.button === 1;
            const isSpacePan = event.button === 0 && spaceHeld;
            if ((!isMiddlePan && !isSpacePan) || !canPan()) {
              return;
            }

            event.preventDefault();
            activePointerId = event.pointerId;
            startX = event.clientX;
            startY = event.clientY;
            startScrollLeft = viewport.scrollLeft;
            startScrollTop = viewport.scrollTop;
            viewport.setPointerCapture(activePointerId);
            viewport.classList.add('panning');
          });

          viewport.addEventListener('pointermove', event => {
            if (event.pointerId !== activePointerId) {
              return;
            }

            event.preventDefault();
            viewport.scrollLeft = startScrollLeft - (event.clientX - startX);
            viewport.scrollTop = startScrollTop - (event.clientY - startY);
          });

          viewport.addEventListener('pointerup', stopPan);
          viewport.addEventListener('pointercancel', stopPan);
          viewport.addEventListener('lostpointercapture', stopPan);
          viewport.addEventListener('pointerleave', stopPan);
          viewport.addEventListener('dragstart', event => event.preventDefault());
          viewport.addEventListener('selectstart', event => event.preventDefault());
          viewport.addEventListener('auxclick', event => {
            if (event.button === 1) {
              event.preventDefault();
            }
          });

          window.addEventListener('keydown', event => {
            if (event.code === 'Space') {
              spaceHeld = true;
              event.preventDefault();
              refreshCursor();
            }
          });

          window.addEventListener('keyup', event => {
            if (event.code === 'Space') {
              spaceHeld = false;
              stopPan();
              refreshCursor();
            }
          });

          window.addEventListener('blur', () => {
            spaceHeld = false;
            stopPan();
            refreshCursor();
          });

          const applyInitialScroll = () => {
            const left = Number.parseFloat(document.body.dataset.initialScrollLeft || '0');
            const top = Number.parseFloat(document.body.dataset.initialScrollTop || '0');
            viewport.scrollLeft = Number.isFinite(left) ? left : 0;
            viewport.scrollTop = Number.isFinite(top) ? top : 0;
            refreshCursor();
          };

          image.addEventListener('load', refreshCursor, { once: true });
          new ResizeObserver(refreshCursor).observe(viewport);
          requestAnimationFrame(applyInitialScroll);
        })();
        """;

    public string Build(
        string validatedSvg,
        double renderedWidth,
        double renderedHeight,
        string bridgeToken,
        PreviewScrollPosition? initialScroll = null)
    {
        ArgumentNullException.ThrowIfNull(validatedSvg);
        ArgumentNullException.ThrowIfNull(bridgeToken);
        if (bridgeToken.Length != 32 || bridgeToken.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "The preview bridge token must contain exactly 32 hexadecimal characters.",
                nameof(bridgeToken));
        }
        if (!double.IsFinite(renderedWidth) || renderedWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(renderedWidth));
        }
        if (!double.IsFinite(renderedHeight) || renderedHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(renderedHeight));
        }

        string encodedSvg = Convert.ToBase64String(Encoding.UTF8.GetBytes(validatedSvg));
        string widthCss = renderedWidth.ToString("0.###", CultureInfo.InvariantCulture);
        string heightCss = renderedHeight.ToString("0.###", CultureInfo.InvariantCulture);
        string stageWidthCss = (renderedWidth + (PreviewZoomCalculator.CanvasPadding * 2))
            .ToString("0.###", CultureInfo.InvariantCulture);
        string stageHeightCss = (renderedHeight + (PreviewZoomCalculator.CanvasPadding * 2))
            .ToString("0.###", CultureInfo.InvariantCulture);
        PreviewScrollPosition scroll = initialScroll ?? PreviewScrollPosition.Origin;
        string initialScrollLeft = ToSafeScrollCss(scroll.Left);
        string initialScrollTop = ToSafeScrollCss(scroll.Top);
        string scriptHash = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(HostScript)));

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'; connect-src 'none'; font-src 'none'; media-src 'none'; object-src 'none'; script-src 'sha256-{{scriptHash}}'">
              <meta name="referrer" content="no-referrer">
              <meta name="color-scheme" content="light">
              <style>
                * { box-sizing: border-box; }
                html, body {
                  width: 100%;
                  height: 100%;
                  margin: 0;
                  overflow: hidden;
                  color-scheme: only light;
                  background-color: #f8fafc;
                }
                body {
                  background-color: #f8fafc;
                  background-image:
                    linear-gradient(45deg, #e2e8f0 25%, transparent 25%),
                    linear-gradient(-45deg, #e2e8f0 25%, transparent 25%),
                    linear-gradient(45deg, transparent 75%, #e2e8f0 75%),
                    linear-gradient(-45deg, transparent 75%, #e2e8f0 75%);
                  background-size: 24px 24px;
                  background-position: 0 0, 0 12px, 12px -12px, -12px 0;
                  background-attachment: fixed;
                }
                .preview-viewport {
                  width: 100%;
                  height: 100%;
                  overflow: auto;
                  cursor: default;
                  overscroll-behavior: contain;
                  user-select: none;
                }
                .preview-viewport.can-pan.space-held {
                  cursor: grab;
                }
                .preview-viewport.panning {
                  cursor: grabbing;
                }
                main {
                  position: relative;
                  width: {{stageWidthCss}}px;
                  height: {{stageHeightCss}}px;
                  min-width: 100%;
                  min-height: 100%;
                  padding: 24px;
                }
                img {
                  display: block;
                  position: absolute;
                  left: 50%;
                  top: 50%;
                  transform: translate(-50%, -50%);
                  width: {{widthCss}}px;
                  height: {{heightCss}}px;
                  max-width: none;
                  max-height: none;
                  pointer-events: none;
                  user-select: none;
                }
              </style>
            </head>
            <body data-bridge-token="{{bridgeToken}}"
                  data-initial-scroll-left="{{initialScrollLeft}}"
                  data-initial-scroll-top="{{initialScrollTop}}">
              <div class="preview-viewport">
                <main aria-label="SVG preview">
                  <img alt="Rendered SVG preview" draggable="false" src="data:image/svg+xml;base64,{{encodedSvg}}">
                </main>
              </div>
              <script>{{HostScript}}</script>
            </body>
            </html>
            """;
    }

    private static string ToSafeScrollCss(double value)
    {
        return (double.IsFinite(value) ? Math.Max(0, value) : 0)
            .ToString("0.###", CultureInfo.InvariantCulture);
    }
}
