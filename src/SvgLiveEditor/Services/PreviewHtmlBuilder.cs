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
          const stage = document.querySelector('main');
          const image = document.querySelector('img');
          const bridge = window.chrome && window.chrome.webview;
          const bridgeToken = document.body.dataset.bridgeToken;
          let spaceHeld = false;
          let panModeEnabled = false;
          let activePointerId = null;
          let activePanButton = null;
          let startX = 0;
          let startY = 0;
          let startScrollLeft = 0;
          let startScrollTop = 0;
          let lastPointerX = viewport.clientWidth / 2;
          let lastPointerY = viewport.clientHeight / 2;
          let viewportPostScheduled = false;
          let initialViewportApplied = false;

          const canPan = () =>
            viewport.scrollWidth > viewport.clientWidth ||
            viewport.scrollHeight > viewport.clientHeight;

          const refreshCursor = () => {
            viewport.classList.toggle('can-pan', canPan());
            viewport.classList.toggle('space-held', spaceHeld && canPan());
            viewport.classList.toggle(
              'pan-mode',
              panModeEnabled && canPan());
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
            activePanButton = null;
            viewport.classList.remove('panning');
            refreshCursor();
          };

          const rememberPointer = event => {
            const rect = viewport.getBoundingClientRect();
            lastPointerX = Math.max(0, Math.min(
              viewport.clientWidth,
              event.clientX - rect.left));
            lastPointerY = Math.max(0, Math.min(
              viewport.clientHeight,
              event.clientY - rect.top));
          };

          const postZoomRequest = (direction, anchorX, anchorY) => {
            if (!bridge) {
              return;
            }

            const safeAnchorX = Math.max(
              0,
              Math.min(viewport.clientWidth, anchorX));
            const safeAnchorY = Math.max(
              0,
              Math.min(viewport.clientHeight, anchorY));
            bridge.postMessage({
              type: 'zoom',
              token: bridgeToken,
              direction,
              contentX: Math.max(0, Math.min(1,
                (viewport.scrollLeft + safeAnchorX) / Math.max(1, viewport.scrollWidth))),
              contentY: Math.max(0, Math.min(1,
                (viewport.scrollTop + safeAnchorY) / Math.max(1, viewport.scrollHeight))),
              anchorX: safeAnchorX,
              anchorY: safeAnchorY,
              viewportWidth: viewport.clientWidth,
              viewportHeight: viewport.clientHeight
            });
          };

          const postViewportState = () => {
            viewportPostScheduled = false;
            if (!bridge || !initialViewportApplied) {
              return;
            }

            bridge.postMessage({
              type: 'viewport',
              token: bridgeToken,
              centerX: Math.max(0, Math.min(1,
                (viewport.scrollLeft + (viewport.clientWidth / 2)) /
                Math.max(1, viewport.scrollWidth))),
              centerY: Math.max(0, Math.min(1,
                (viewport.scrollTop + (viewport.clientHeight / 2)) /
                Math.max(1, viewport.scrollHeight)))
            });
          };

          const scheduleViewportState = () => {
            if (!viewportPostScheduled) {
              viewportPostScheduled = true;
              requestAnimationFrame(postViewportState);
            }
          };

          const postPanCommand = command => {
            if (bridge) {
              bridge.postMessage({
                type: 'panCommand',
                token: bridgeToken,
                command
              });
            }
          };

          const postCopyCommand = () => {
            if (bridge) {
              bridge.postMessage({
                type: 'copyCommand',
                token: bridgeToken
              });
            }
          };

          const postContextMenuRequest = () => {
            if (bridge) {
              bridge.postMessage({
                type: 'contextMenu',
                token: bridgeToken,
                x: lastPointerX,
                y: lastPointerY,
                viewportWidth: viewport.clientWidth,
                viewportHeight: viewport.clientHeight
              });
            }
          };

          const postPngError = requestId => {
            if (bridge) {
              bridge.postMessage({
                type: 'pngError',
                token: bridgeToken,
                requestId
              });
            }
          };

          const renderPng = message => {
            if (!image.complete || image.naturalWidth <= 0 ||
                image.naturalHeight <= 0) {
              postPngError(message.requestId);
              return;
            }

            const canvas = document.createElement('canvas');
            canvas.width = message.width;
            canvas.height = message.height;
            const context = canvas.getContext('2d', { alpha: true });
            if (!context) {
              postPngError(message.requestId);
              return;
            }

            try {
              context.clearRect(0, 0, message.width, message.height);
              context.drawImage(image, 0, 0, message.width, message.height);
              const dataUrl = canvas.toDataURL('image/png');
              const prefix = 'data:image/png;base64,';
              if (!dataUrl.startsWith(prefix) ||
                  dataUrl.length - prefix.length > 53333336) {
                postPngError(message.requestId);
                return;
              }

              bridge.postMessage({
                type: 'png',
                token: bridgeToken,
                requestId: message.requestId,
                mimeType: 'image/png',
                width: message.width,
                height: message.height,
                payload: dataUrl.slice(prefix.length)
              });
            } catch {
              postPngError(message.requestId);
            } finally {
              canvas.width = 0;
              canvas.height = 0;
            }
          };

          const restoreViewportCenter = (centerX, centerY) => {
            const safeCenterX = Number.isFinite(centerX)
              ? Math.max(0, Math.min(1, centerX))
              : 0.5;
            const safeCenterY = Number.isFinite(centerY)
              ? Math.max(0, Math.min(1, centerY))
              : 0.5;
            viewport.scrollLeft = Math.max(0, Math.min(
              viewport.scrollWidth - viewport.clientWidth,
              (safeCenterX * viewport.scrollWidth) - (viewport.clientWidth / 2)));
            viewport.scrollTop = Math.max(0, Math.min(
              viewport.scrollHeight - viewport.clientHeight,
              (safeCenterY * viewport.scrollHeight) - (viewport.clientHeight / 2)));
            refreshCursor();
            scheduleViewportState();
          };

          if (bridge) {
            bridge.addEventListener('message', event => {
              const message = event.data;
              if (!message || typeof message !== 'object' ||
                  message.token !== bridgeToken) {
                return;
              }

              if (message.type === 'zoomState' &&
                  Object.keys(message).length === 6 &&
                  Number.isFinite(message.renderedWidth) &&
                  message.renderedWidth > 0 &&
                  message.renderedWidth <= 10000000 &&
                  Number.isFinite(message.renderedHeight) &&
                  message.renderedHeight > 0 &&
                  message.renderedHeight <= 10000000 &&
                  Number.isFinite(message.centerX) &&
                  message.centerX >= 0 &&
                  message.centerX <= 1 &&
                  Number.isFinite(message.centerY) &&
                  message.centerY >= 0 &&
                  message.centerY <= 1) {
                image.style.width = `${message.renderedWidth}px`;
                image.style.height = `${message.renderedHeight}px`;
                stage.style.width = `${message.renderedWidth + 48}px`;
                stage.style.height = `${message.renderedHeight + 48}px`;
                requestAnimationFrame(() =>
                  restoreViewportCenter(message.centerX, message.centerY));
                return;
              }

              if (message.type === 'panState' &&
                  Object.keys(message).length === 3 &&
                  typeof message.enabled === 'boolean') {
                panModeEnabled = message.enabled;
                stopPan();
                refreshCursor();
                return;
              }

              if (message.type === 'copyPng' &&
                  Object.keys(message).length === 5 &&
                  typeof message.requestId === 'string' &&
                  /^[0-9a-fA-F]{32}$/.test(message.requestId) &&
                  Number.isInteger(message.width) &&
                  message.width > 0 &&
                  message.width <= 4096 &&
                  Number.isInteger(message.height) &&
                  message.height > 0 &&
                  message.height <= 4096 &&
                  message.width * message.height <= 8000000) {
                renderPng(message);
              }
            });
          }

          const handleWheel = event => {
            rememberPointer(event);
            if (event.ctrlKey) {
              event.preventDefault();
              postZoomRequest(
                event.deltaY < 0 ? 'in' : 'out',
                lastPointerX,
                lastPointerY);
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
            rememberPointer(event);
            const isMiddlePan = event.button === 1;
            const isSpacePan = event.button === 0 && spaceHeld;
            const isCtrlPan = event.button === 0 && event.ctrlKey;
            const isModePan = event.button === 0 && panModeEnabled;
            if ((!isMiddlePan && !isSpacePan &&
                 !isCtrlPan && !isModePan) || !canPan()) {
              return;
            }

            event.preventDefault();
            activePointerId = event.pointerId;
            activePanButton = event.button;
            startX = event.clientX;
            startY = event.clientY;
            startScrollLeft = viewport.scrollLeft;
            startScrollTop = viewport.scrollTop;
            viewport.setPointerCapture(activePointerId);
            viewport.classList.add('panning');
          });

          viewport.addEventListener('pointermove', event => {
            rememberPointer(event);
            if (event.pointerId !== activePointerId) {
              return;
            }

            const requiredButtonMask = activePanButton === 1 ? 4 : 1;
            if ((event.buttons & requiredButtonMask) === 0) {
              stopPan(event);
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
          viewport.addEventListener('scroll', scheduleViewportState);
          window.addEventListener('pointerup', stopPan, true);
          window.addEventListener('pointercancel', stopPan, true);
          viewport.addEventListener('dragstart', event => event.preventDefault());
          viewport.addEventListener('selectstart', event => event.preventDefault());
          viewport.addEventListener('auxclick', event => {
            if (event.button === 1) {
              event.preventDefault();
            }
          });
          viewport.addEventListener('contextmenu', event => {
            event.preventDefault();
            rememberPointer(event);
            viewport.focus({ preventScroll: true });
            postContextMenuRequest();
          });

          window.addEventListener('keydown', event => {
            if (event.code === 'KeyC' &&
                event.ctrlKey && !event.shiftKey &&
                !event.altKey && !event.metaKey) {
              event.preventDefault();
              postCopyCommand();
            } else if (event.code === 'Space') {
              spaceHeld = true;
              event.preventDefault();
              refreshCursor();
            } else if (event.code === 'KeyH' &&
                       !event.ctrlKey && !event.altKey &&
                       !event.metaKey && !event.shiftKey &&
                       !event.repeat) {
              event.preventDefault();
              postPanCommand('toggle');
            } else if (event.code === 'Escape') {
              event.preventDefault();
              postPanCommand('exit');
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

          const applyInitialViewport = () => {
            const centerX = Number.parseFloat(
              document.body.dataset.initialCenterX || '0.5');
            const centerY = Number.parseFloat(
              document.body.dataset.initialCenterY || '0.5');
            initialViewportApplied = true;
            restoreViewportCenter(centerX, centerY);
          };

          const initializeViewport = () =>
            requestAnimationFrame(() => requestAnimationFrame(applyInitialViewport));
          if (image.complete) {
            initializeViewport();
          } else {
            image.addEventListener('load', initializeViewport, { once: true });
          }
          new ResizeObserver(() => {
            refreshCursor();
            scheduleViewportState();
          }).observe(viewport);
          document.body.dataset.hostScriptReady = 'true';
        })();
        """;

    public string Build(
        string validatedSvg,
        double renderedWidth,
        double renderedHeight,
        string bridgeToken,
        PreviewViewportPosition? initialViewport = null)
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
        PreviewViewportPosition viewport =
            initialViewport ?? PreviewViewportPosition.Center;
        string initialCenterX = ToSafeNormalized(viewport.CenterX);
        string initialCenterY = ToSafeNormalized(viewport.CenterY);
        // HTML parsers normalize inline-script line endings to LF before CSP hash
        // verification. Normalize once and use the exact same bytes for the hash
        // and script body so Windows CRLF checkouts cannot disable the host script.
        string normalizedHostScript = HostScript
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        string scriptHash = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(normalizedHostScript)));

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
                  outline: none;
                  overscroll-behavior: contain;
                  user-select: none;
                }
                .preview-viewport:focus-visible {
                  box-shadow: inset 0 0 0 3px #2563eb;
                }
                .preview-viewport.can-pan.space-held,
                .preview-viewport.can-pan.pan-mode {
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
                  data-initial-center-x="{{initialCenterX}}"
                  data-initial-center-y="{{initialCenterY}}">
              <div class="preview-viewport"
                   tabindex="0"
                   role="region"
                   aria-label="Live SVG preview">
                <main aria-label="SVG preview">
                  <img alt="Rendered SVG preview" draggable="false" src="data:image/svg+xml;base64,{{encodedSvg}}">
                </main>
              </div>
              <script>{{normalizedHostScript}}</script>
            </body>
            </html>
            """;
    }

    private static string ToSafeNormalized(double value)
    {
        return (double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0.5)
            .ToString("0.###", CultureInfo.InvariantCulture);
    }
}
