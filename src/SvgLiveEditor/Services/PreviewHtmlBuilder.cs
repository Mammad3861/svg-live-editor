using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class PreviewHtmlBuilder
{
    public const string SelectionAccentColor = "#2563eb";
    public const string SelectionAccentStrongColor = "#1d4ed8";
    public const int ResizeHandleSizeCssPixels = 10;

    private const string HostScript = """
        (() => {
          'use strict';
          const viewport = document.querySelector('.preview-viewport');
          const stage = document.querySelector('main');
          const image = document.querySelector('img');
          const selectionOverlay = document.querySelector(
            '.selection-overlay');
          const resizeHandleLayer = document.querySelector(
            '.resize-handle-layer');
          const bridge = window.chrome && window.chrome.webview;
          const bridgeToken = document.body.dataset.bridgeToken;
          const sourceRevision = Number.parseInt(
            document.body.dataset.sourceRevision || '-1',
            10);
          let spaceHeld = false;
          let panModeEnabled = false;
          let minimumHorizontalDragDistance = null;
          let minimumVerticalDragDistance = null;
          let activePointerId = null;
          let activePanButton = null;
          let activeDirectDrag = null;
          let activeVisualGesture = null;
          let activeResizeGesture = null;
          let activeVisualSelection = null;
          let startX = 0;
          let startY = 0;
          let startScrollLeft = 0;
          let startScrollTop = 0;
          let lastPointerX = viewport.clientWidth / 2;
          let lastPointerY = viewport.clientHeight / 2;
          let viewportPostScheduled = false;
          let initialViewportApplied = false;
          let artworkHovered = false;

          const canPan = () =>
            viewport.scrollWidth > viewport.clientWidth ||
            viewport.scrollHeight > viewport.clientHeight;

          const refreshCursor = () => {
            viewport.classList.toggle('can-pan', canPan());
            viewport.classList.toggle('space-held', spaceHeld && canPan());
            viewport.classList.toggle(
              'pan-mode',
              panModeEnabled && canPan());
            image.classList.toggle(
              'drag-ready',
              !panModeEnabled &&
              minimumHorizontalDragDistance !== null &&
              minimumVerticalDragDistance !== null);
            resizeHandleLayer.hidden = panModeEnabled;
            viewport.classList.toggle('select-mode', !panModeEnabled);
            viewport.classList.toggle(
              'artwork-hovered',
              artworkHovered && activeVisualSelection !== null);
            const selectionState = activeResizeGesture !== null
              ? 'resizing'
              : activeVisualGesture !== null
                ? 'moving'
                : 'selected';
            selectionOverlay.dataset.state = activeVisualSelection === null
              ? 'none'
              : selectionState;
            resizeHandleLayer.dataset.state = activeVisualSelection === null
              ? 'none'
              : selectionState;
            resizeHandleLayer.querySelectorAll('.resize-handle')
              .forEach(handle => handle.classList.toggle(
                'active',
                activeResizeGesture !== null &&
                handle.dataset.handle === activeResizeGesture.handle));
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

          const postImageState = state => {
            if (!bridge) {
              return;
            }

            const loaded = state === 'loaded';
            bridge.postMessage({
              type: 'imageState',
              token: bridgeToken,
              sourceRevision,
              state,
              naturalWidth: loaded ? image.naturalWidth : 0,
              naturalHeight: loaded ? image.naturalHeight : 0
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
                viewportHeight: viewport.clientHeight,
                sourceRevision,
                selectionId: activeVisualSelection?.selectionId || ''
              });
            }
          };

          const postDirectDragArm = (gestureId, event) => {
            if (!bridge) {
              return;
            }

            bridge.postMessage({
              type: 'directDrag',
              token: bridgeToken,
              action: 'arm',
              gestureId,
              x: lastPointerX,
              y: lastPointerY,
              viewportWidth: viewport.clientWidth,
              viewportHeight: viewport.clientHeight,
              button: event.button,
              startedOnArtwork: isArtworkUnderPointer(event),
              isPrimary: event.isPrimary,
              pointerType: event.pointerType,
              ctrlKey: event.ctrlKey,
              shiftKey: event.shiftKey,
              altKey: event.altKey,
              metaKey: event.metaKey,
              spaceHeld
            });
          };

          const postDirectDragSignal = (action, gestureId) => {
            if (!bridge) {
              return;
            }

            bridge.postMessage({
              type: 'directDrag',
              token: bridgeToken,
              action,
              gestureId,
              x: lastPointerX,
              y: lastPointerY,
              viewportWidth: viewport.clientWidth,
              viewportHeight: viewport.clientHeight
            });
          };

          const postVisualPointer = (phase, gesture, event = null) => {
            if (!bridge || !Number.isSafeInteger(sourceRevision) ||
                sourceRevision < 0) {
              return;
            }

            const imageRect = image.getBoundingClientRect();
            const viewportRect = viewport.getBoundingClientRect();
            bridge.postMessage({
              type: 'visualPointer',
              token: bridgeToken,
              sourceRevision,
              phase,
              gestureId: gesture.gestureId,
              x: lastPointerX,
              y: lastPointerY,
              viewportWidth: viewport.clientWidth,
              viewportHeight: viewport.clientHeight,
              imageLeft: imageRect.left - viewportRect.left,
              imageTop: imageRect.top - viewportRect.top,
              imageWidth: imageRect.width,
              imageHeight: imageRect.height,
              button: 0,
              buttons: event ? event.buttons : 0,
              ctrlKey: event ? event.ctrlKey : false,
              shiftKey: event ? event.shiftKey : false,
              altKey: event ? event.altKey : false,
              metaKey: event ? event.metaKey : false,
              spaceHeld,
              pointerType: event ? event.pointerType : 'mouse',
              isPrimary: event ? event.isPrimary : true
            });
          };

          const postVisualResizePointer = (
              phase,
              gesture,
              event = null) => {
            if (!bridge || !Number.isSafeInteger(sourceRevision) ||
                sourceRevision < 0 || activeVisualSelection === null ||
                gesture.selectionId !== activeVisualSelection.selectionId) {
              return;
            }

            const imageRect = image.getBoundingClientRect();
            const viewportRect = viewport.getBoundingClientRect();
            bridge.postMessage({
              type: 'visualResizePointer',
              token: bridgeToken,
              sourceRevision,
              selectionId: gesture.selectionId,
              phase,
              gestureId: gesture.gestureId,
              handle: gesture.handle,
              x: lastPointerX,
              y: lastPointerY,
              viewportWidth: viewport.clientWidth,
              viewportHeight: viewport.clientHeight,
              imageLeft: imageRect.left - viewportRect.left,
              imageTop: imageRect.top - viewportRect.top,
              imageWidth: imageRect.width,
              imageHeight: imageRect.height,
              button: 0,
              buttons: event ? event.buttons : 0,
              ctrlKey: event ? event.ctrlKey : false,
              shiftKey: event ? event.shiftKey : false,
              altKey: event ? event.altKey : false,
              metaKey: event ? event.metaKey : false,
              spaceHeld,
              pointerType: event ? event.pointerType : 'mouse',
              isTrusted: event ? event.isTrusted : true,
              isPrimary: event ? event.isPrimary : true
            });
          };

          const postVisualNudge = (deltaX, deltaY) => {
            if (bridge && Number.isSafeInteger(sourceRevision) &&
                sourceRevision >= 0) {
              bridge.postMessage({
                type: 'visualNudge',
                token: bridgeToken,
                sourceRevision,
                deltaX,
                deltaY
              });
            }
          };

          const createGestureId = () => {
            if (!globalThis.crypto ||
                typeof globalThis.crypto.getRandomValues !== 'function') {
              return null;
            }

            const bytes = new Uint8Array(16);
            globalThis.crypto.getRandomValues(bytes);
            return Array.from(
              bytes,
                value => value.toString(16).padStart(2, '0')).join('');
          };

          const getResizeHandle = target =>
            target instanceof HTMLElement &&
            target.classList.contains('resize-handle')
              ? target
              : null;

          const isArtworkUnderPointer = event => {
            if (event.target === image) {
              return true;
            }
            const handle = getResizeHandle(event.target);
            if (handle === null) {
              return false;
            }

            const previousPointerEvents = handle.style.pointerEvents;
            handle.style.pointerEvents = 'none';
            const underneath = document.elementFromPoint(
              event.clientX,
              event.clientY);
            handle.style.pointerEvents = previousPointerEvents;
            return underneath === image;
          };

          const choosePointerAction = event => {
            if (event.button === 1) {
              return 'pan';
            }
            if (event.button !== 0) {
              return 'none';
            }
            if (spaceHeld || event.ctrlKey || panModeEnabled) {
              return 'pan';
            }
            if (!event.isTrusted || !event.isPrimary ||
                event.pointerType !== 'mouse') {
              return 'none';
            }
            const resizeHandle = getResizeHandle(event.target);
            if (event.altKey && !event.shiftKey && !event.metaKey &&
                isArtworkUnderPointer(event)) {
              return 'drag';
            }
            if (event.altKey || event.metaKey) {
              return 'none';
            }
            if (resizeHandle !== null) {
              return 'resize';
            }
            if (event.shiftKey) {
              return 'none';
            }
            return 'visual';
          };

          const stopDirectDrag = (event, notify = true) => {
            if (activeDirectDrag === null ||
                (event && event.pointerId !== activeDirectDrag.pointerId)) {
              return;
            }

            const stopped = activeDirectDrag;
            activeDirectDrag = null;
            image.classList.remove('drag-armed');
            if (image.hasPointerCapture(stopped.pointerId)) {
              image.releasePointerCapture(stopped.pointerId);
            }
            if (notify) {
              postDirectDragSignal('cancel', stopped.gestureId);
            }
            refreshCursor();
          };

          const stopVisualGesture = (event, notify = true) => {
            if (activeVisualGesture === null ||
                (event &&
                 event.pointerId !== activeVisualGesture.pointerId)) {
              return;
            }

            const stopped = activeVisualGesture;
            activeVisualGesture = null;
            if (viewport.hasPointerCapture(stopped.pointerId)) {
              viewport.releasePointerCapture(stopped.pointerId);
            }
            if (notify) {
              postVisualPointer('cancel', stopped, event);
            }
            refreshCursor();
          };

          const stopResizeGesture = (event, notify = true) => {
            if (activeResizeGesture === null ||
                (event &&
                 event.pointerId !== activeResizeGesture.pointerId)) {
              return;
            }

            const stopped = activeResizeGesture;
            activeResizeGesture = null;
            if (viewport.hasPointerCapture(stopped.pointerId)) {
              viewport.releasePointerCapture(stopped.pointerId);
            }
            if (notify) {
              postVisualResizePointer('cancel', stopped, event);
            }
            refreshCursor();
          };

          const renderVisualSelection = message => {
            if (activeResizeGesture !== null &&
                (!message.visible ||
                 message.selectionId !== activeResizeGesture.selectionId)) {
              stopResizeGesture();
            }
            selectionOverlay.replaceChildren();
            resizeHandleLayer.replaceChildren();
            activeVisualSelection = null;
            if (!message.visible) {
              refreshCursor();
              return;
            }

            const namespace = 'http://www.w3.org/2000/svg';
            const deltaX = message.deltaX;
            const deltaY = message.deltaY;
            let shape;
            if (message.kind === 'line') {
              shape = document.createElementNS(namespace, 'line');
              shape.setAttribute('x1', message.x1 + deltaX);
              shape.setAttribute('y1', message.y1 + deltaY);
              shape.setAttribute('x2', message.x2 + deltaX);
              shape.setAttribute('y2', message.y2 + deltaY);
            } else if (message.kind === 'ellipse' ||
                       message.kind === 'circle') {
              shape = document.createElementNS(namespace, 'ellipse');
              shape.setAttribute(
                'cx',
                ((message.x1 + message.x2) / 2) + deltaX);
              shape.setAttribute(
                'cy',
                ((message.y1 + message.y2) / 2) + deltaY);
              shape.setAttribute(
                'rx',
                Math.abs(message.x2 - message.x1) / 2);
              shape.setAttribute(
                'ry',
                Math.abs(message.y2 - message.y1) / 2);
            } else {
              shape = document.createElementNS(namespace, 'rect');
              shape.setAttribute(
                'x',
                Math.min(message.x1, message.x2) + deltaX);
              shape.setAttribute(
                'y',
                Math.min(message.y1, message.y2) + deltaY);
              shape.setAttribute(
                'width',
                Math.abs(message.x2 - message.x1));
              shape.setAttribute(
                'height',
                Math.abs(message.y2 - message.y1));
            }
            shape.classList.add('selection-shape');
            selectionOverlay.appendChild(shape);

            activeVisualSelection = {
              selectionId: message.selectionId,
              handles: message.handles,
              deltaX,
              deltaY
            };
            for (const handleDefinition of message.handles) {
              const handle = document.createElement('div');
              handle.className = 'resize-handle';
              handle.dataset.handle = handleDefinition.id;
              handle.title = `Resize ${handleDefinition.id}`;
              resizeHandleLayer.appendChild(handle);
            }
            positionResizeHandles();
            requestAnimationFrame(positionResizeHandles);
            refreshCursor();
          };

          const positionResizeHandles = () => {
            if (activeVisualSelection === null) {
              return;
            }

            const matrix = selectionOverlay.getScreenCTM();
            if (matrix === null) {
              return;
            }
            const stageRect = stage.getBoundingClientRect();
            const handles = resizeHandleLayer.querySelectorAll(
              '.resize-handle');
            handles.forEach((handle, index) => {
              const definition = activeVisualSelection.handles[index];
              if (!definition) {
                return;
              }
              const point = new DOMPoint(
                definition.x + activeVisualSelection.deltaX,
                definition.y + activeVisualSelection.deltaY)
                .matrixTransform(matrix);
              handle.style.left = `${point.x - stageRect.left}px`;
              handle.style.top = `${point.y - stageRect.top}px`;
            });
          };

          const areHandlesAllowedForKind = (kind, handles) => {
            const allowed = kind === 'rect' || kind === 'ellipse'
              ? ['top-left', 'top', 'top-right', 'right',
                 'bottom-right', 'bottom', 'bottom-left', 'left']
              : kind === 'circle'
                ? ['top', 'right', 'bottom', 'left']
                : kind === 'line'
                  ? ['start', 'end']
                  : [];
            return handles.every(handle => allowed.includes(handle.id));
          };

          const isSafeFontFamily = value => {
            if (typeof value !== 'string' ||
                value.length === 0 || value.length > 256 ||
                /[\u0000-\u001f\u007f]/.test(value) ||
                /url\s*\(/i.test(value) ||
                /[;{}<>\\@]/.test(value)) {
              return false;
            }
            return value.split(',').every(part => part.trim().length > 0);
          };

          const measureTextItems = message => {
            const svgNamespace = 'http://www.w3.org/2000/svg';
            const measurementSurface = document.createElementNS(
              svgNamespace,
              'svg');
            measurementSurface.setAttribute('aria-hidden', 'true');
            measurementSurface.setAttribute('focusable', 'false');
            measurementSurface.style.position = 'fixed';
            measurementSurface.style.left = '-100000px';
            measurementSurface.style.top = '-100000px';
            measurementSurface.style.width = '1px';
            measurementSurface.style.height = '1px';
            measurementSurface.style.overflow = 'visible';
            measurementSurface.style.opacity = '0';
            measurementSurface.style.pointerEvents = 'none';
            document.body.appendChild(measurementSurface);
            let results;
            try {
              results = message.items.map(item => {
                const measuredText = document.createElementNS(
                  svgNamespace,
                  'text');
                measuredText.setAttribute('x', String(item.x));
                measuredText.setAttribute('y', String(item.y));
                measuredText.setAttribute('font-size', String(item.fontSize));
                measuredText.setAttribute('font-family', item.fontFamily);
                measuredText.setAttribute('font-weight', item.fontWeight);
                measuredText.setAttribute('font-style', item.fontStyle);
                measuredText.setAttribute('text-anchor', item.textAnchor);
                measuredText.setAttribute('direction', item.direction);
                measuredText.setAttribute('unicode-bidi', item.unicodeBidi);
                measuredText.textContent = item.text;
                measurementSurface.appendChild(measuredText);
                try {
                  const bounds = measuredText.getBBox();
                  const left = bounds.x;
                  const top = bounds.y;
                  const right = bounds.x + bounds.width;
                  const bottom = bounds.y + bounds.height;
                  if (![left, top, right, bottom].every(Number.isFinite) ||
                      right <= left || bottom <= top ||
                      [left, top, right, bottom]
                        .some(value => Math.abs(value) > 1000000000)) {
                    throw new Error('Invalid text bounds');
                  }
                  return {
                    index: item.index,
                    success: true,
                    left,
                    top,
                    right,
                    bottom
                  };
                } catch {
                  return {
                    index: item.index,
                    success: false,
                    left: 0,
                    top: 0,
                    right: 0,
                    bottom: 0
                  };
                } finally {
                  measuredText.remove();
                }
              });
            } finally {
              measurementSurface.remove();
            }
            bridge.postMessage({
              type: 'textMeasurements',
              token: bridgeToken,
              sourceRevision,
              requestId: message.requestId,
              results
            });
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
                selectionOverlay.style.width =
                  `${message.renderedWidth}px`;
                selectionOverlay.style.height =
                  `${message.renderedHeight}px`;
                stage.style.width = `${message.renderedWidth + 48}px`;
                stage.style.height = `${message.renderedHeight + 48}px`;
                requestAnimationFrame(() => {
                  restoreViewportCenter(message.centerX, message.centerY);
                  positionResizeHandles();
                });
                return;
              }

              if (message.type === 'panState' &&
                  Object.keys(message).length === 5 &&
                  typeof message.enabled === 'boolean' &&
                  Number.isFinite(message.minimumHorizontalDragDistance) &&
                  message.minimumHorizontalDragDistance > 0 &&
                  message.minimumHorizontalDragDistance <= 1000 &&
                  Number.isFinite(message.minimumVerticalDragDistance) &&
                  message.minimumVerticalDragDistance > 0 &&
                  message.minimumVerticalDragDistance <= 1000) {
                panModeEnabled = message.enabled;
                minimumHorizontalDragDistance =
                  message.minimumHorizontalDragDistance;
                minimumVerticalDragDistance =
                  message.minimumVerticalDragDistance;
                stopPan();
                stopDirectDrag();
                stopResizeGesture(null, false);
                stopVisualGesture(null, false);
                refreshCursor();
                return;
              }

              if (message.type === 'horizontalScroll' &&
                  Object.keys(message).length === 3 &&
                  Number.isFinite(message.deltaX) &&
                  message.deltaX !== 0 &&
                  Math.abs(message.deltaX) <= 10000) {
                viewport.scrollBy({
                  left: message.deltaX,
                  top: 0,
                  behavior: 'auto'
                });
                return;
              }

              if (message.type === 'visualSelection' &&
                  Object.keys(message).length === 13 &&
                  Number.isSafeInteger(message.sourceRevision) &&
                  message.sourceRevision === sourceRevision &&
                  typeof message.visible === 'boolean' &&
                  ['none', 'rect', 'circle', 'ellipse', 'line', 'text']
                    .includes(message.kind) &&
                  Number.isFinite(message.x1) &&
                  Math.abs(message.x1) <= 1000000000 &&
                  Number.isFinite(message.y1) &&
                  Math.abs(message.y1) <= 1000000000 &&
                  Number.isFinite(message.x2) &&
                  Math.abs(message.x2) <= 1000000000 &&
                  Number.isFinite(message.y2) &&
                  Math.abs(message.y2) <= 1000000000 &&
                  Number.isFinite(message.deltaX) &&
                  Math.abs(message.deltaX) <= 1000000000 &&
                  Number.isFinite(message.deltaY) &&
                  Math.abs(message.deltaY) <= 1000000000 &&
                  typeof message.selectionId === 'string' &&
                  Array.isArray(message.handles) &&
                  message.handles.length <= 8 &&
                  message.handles.every(handle =>
                    handle && typeof handle === 'object' &&
                    Object.keys(handle).length === 3 &&
                    typeof handle.id === 'string' &&
                    ['top-left', 'top', 'top-right', 'right',
                     'bottom-right', 'bottom', 'bottom-left', 'left',
                     'start', 'end'].includes(handle.id) &&
                    Number.isFinite(handle.x) &&
                    Math.abs(handle.x) <= 1000000000 &&
                    Number.isFinite(handle.y) &&
                    Math.abs(handle.y) <= 1000000000) &&
                  new Set(message.handles.map(handle => handle.id)).size ===
                    message.handles.length &&
                  areHandlesAllowedForKind(message.kind, message.handles) &&
                  ((message.visible && message.kind !== 'none' &&
                    /^[0-9a-fA-F]{32}$/.test(message.selectionId)) ||
                   (!message.visible && message.kind === 'none' &&
                    message.selectionId === '' &&
                    message.handles.length === 0 &&
                    message.x1 === 0 && message.y1 === 0 &&
                    message.x2 === 0 && message.y2 === 0 &&
                    message.deltaX === 0 && message.deltaY === 0))) {
                renderVisualSelection(message);
                return;
              }

              if (message.type === 'measureText' &&
                  Object.keys(message).length === 5 &&
                  Number.isSafeInteger(message.sourceRevision) &&
                  message.sourceRevision === sourceRevision &&
                  typeof message.requestId === 'string' &&
                  /^[0-9a-fA-F]{32}$/.test(message.requestId) &&
                  Array.isArray(message.items) &&
                  message.items.length > 0 &&
                  message.items.length <= 32 &&
                  message.items.every(item =>
                    item && typeof item === 'object' &&
                    Object.keys(item).length === 11 &&
                    Number.isInteger(item.index) &&
                    item.index >= 0 && item.index < 32 &&
                    typeof item.text === 'string' &&
                    item.text.length > 0 && item.text.length <= 1024 &&
                    !/[\u0000-\u001f\u007f]/.test(item.text) &&
                    Number.isFinite(item.x) &&
                    Math.abs(item.x) <= 1000000000 &&
                    Number.isFinite(item.y) &&
                    Math.abs(item.y) <= 1000000000 &&
                    Number.isFinite(item.fontSize) &&
                    item.fontSize > 0 && item.fontSize <= 1000000000 &&
                    isSafeFontFamily(item.fontFamily) &&
                    ['normal', 'bold', '100', '200', '300', '400',
                     '500', '600', '700', '800', '900']
                      .includes(item.fontWeight) &&
                    ['normal', 'italic', 'oblique']
                      .includes(item.fontStyle) &&
                    ['start', 'middle', 'end']
                      .includes(item.textAnchor) &&
                    ['ltr', 'rtl'].includes(item.direction) &&
                    ['normal', 'embed', 'isolate', 'plaintext']
                      .includes(item.unicodeBidi)) &&
                  new Set(message.items.map(item => item.index)).size ===
                    message.items.length) {
                measureTextItems(message);
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

          const normalizeWheelDelta = (value, deltaMode, pageSize) => {
            if (!Number.isFinite(value) ||
                !Number.isInteger(deltaMode) ||
                !Number.isFinite(pageSize) ||
                pageSize <= 0) {
              return 0;
            }

            let scale;
            if (deltaMode === 0) {
              scale = 1;
            } else if (deltaMode === 1) {
              scale = 16;
            } else if (deltaMode === 2) {
              scale = pageSize;
            } else {
              return 0;
            }

            const pixels = value * scale;
            if (!Number.isFinite(pixels)) {
              return 0;
            }

            const maximumDelta = pageSize * 4;
            return Math.max(-maximumDelta, Math.min(maximumDelta, pixels));
          };

          const handleWheel = event => {
            rememberPointer(event);
            const horizontalDelta = normalizeWheelDelta(
              event.deltaX,
              event.deltaMode,
              viewport.clientWidth);
            const verticalDelta = normalizeWheelDelta(
              event.deltaY,
              event.deltaMode,
              viewport.clientHeight);

            if (event.ctrlKey) {
              event.preventDefault();
              if (verticalDelta !== 0) {
                postZoomRequest(
                  verticalDelta < 0 ? 'in' : 'out',
                  lastPointerX,
                  lastPointerY);
              }
              return;
            }

            const scrollLeft = event.shiftKey
              ? (horizontalDelta !== 0 ? horizontalDelta : verticalDelta)
              : horizontalDelta;
            const scrollTop = event.shiftKey ? 0 : verticalDelta;
            if (scrollLeft === 0 && scrollTop === 0) {
              return;
            }

            event.preventDefault();
            viewport.scrollBy({
              left: scrollLeft,
              top: scrollTop,
              behavior: 'auto'
            });
          };

          // WebView2 must allow Ctrl+Wheel into the renderer before this handler can
          // replace native document zoom with artwork-only zoom.
          window.addEventListener(
            'wheel',
            handleWheel,
            { capture: true, passive: false });

          image.addEventListener('pointerenter', () => {
            artworkHovered = true;
            refreshCursor();
          });
          image.addEventListener('pointerleave', () => {
            artworkHovered = false;
            refreshCursor();
          });

          viewport.addEventListener('pointerdown', event => {
            rememberPointer(event);
            const action = choosePointerAction(event);
            if (action === 'resize') {
              const handle = getResizeHandle(event.target);
              const gestureId = createGestureId();
              if (handle === null || gestureId === null ||
                  activeVisualSelection === null ||
                  !activeVisualSelection.handles.some(
                    item => item.id === handle.dataset.handle)) {
                return;
              }

              event.preventDefault();
              stopPan();
              stopDirectDrag();
              stopVisualGesture();
              stopResizeGesture();
              activeResizeGesture = {
                gestureId,
                pointerId: event.pointerId,
                selectionId: activeVisualSelection.selectionId,
                handle: handle.dataset.handle
              };
              viewport.setPointerCapture(event.pointerId);
              postVisualResizePointer(
                'down',
                activeResizeGesture,
                event);
              refreshCursor();
              return;
            }

            if (action === 'visual') {
              const gestureId = createGestureId();
              if (gestureId === null) {
                return;
              }

              event.preventDefault();
              stopPan();
              stopDirectDrag();
              stopResizeGesture();
              stopVisualGesture();
              activeVisualGesture = {
                gestureId,
                pointerId: event.pointerId
              };
              viewport.setPointerCapture(event.pointerId);
              postVisualPointer(
                'down',
                activeVisualGesture,
                event);
              refreshCursor();
              return;
            }

            if (action === 'drag') {
              if (minimumHorizontalDragDistance === null ||
                  minimumVerticalDragDistance === null) {
                return;
              }

              const gestureId = createGestureId();
              if (gestureId === null) {
                return;
              }

              event.preventDefault();
              stopPan();
              stopResizeGesture();
              stopVisualGesture();
              activeDirectDrag = {
                gestureId,
                pointerId: event.pointerId,
                startX: lastPointerX,
                startY: lastPointerY
              };
              image.setPointerCapture(event.pointerId);
              image.classList.add('drag-armed');
              postDirectDragArm(gestureId, event);
              refreshCursor();
              return;
            }

            if (action !== 'pan' || !canPan()) {
              return;
            }

            event.preventDefault();
            stopDirectDrag();
            stopResizeGesture();
            stopVisualGesture();
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
            if (activeResizeGesture !== null &&
                event.pointerId === activeResizeGesture.pointerId) {
              if (!event.isTrusted ||
                  (event.buttons & 1) === 0 ||
                  spaceHeld || event.ctrlKey || panModeEnabled ||
                  event.altKey || event.metaKey) {
                stopResizeGesture(event);
                return;
              }

              event.preventDefault();
              postVisualResizePointer(
                'move',
                activeResizeGesture,
                event);
              return;
            }

            if (activeVisualGesture !== null &&
                event.pointerId === activeVisualGesture.pointerId) {
              if (!event.isTrusted ||
                  (event.buttons & 1) === 0 ||
                  spaceHeld || event.ctrlKey || panModeEnabled ||
                  event.shiftKey || event.altKey || event.metaKey) {
                stopVisualGesture(event);
                return;
              }

              event.preventDefault();
              postVisualPointer(
                'move',
                activeVisualGesture,
                event);
              return;
            }

            if (activeDirectDrag !== null &&
                event.pointerId === activeDirectDrag.pointerId) {
              if (!event.isTrusted ||
                  (event.buttons & 1) === 0 ||
                  spaceHeld || event.ctrlKey || panModeEnabled ||
                  event.shiftKey || !event.altKey || event.metaKey) {
                stopDirectDrag(event);
                return;
              }

              if (Math.abs(lastPointerX - activeDirectDrag.startX) <
                    minimumHorizontalDragDistance &&
                  Math.abs(lastPointerY - activeDirectDrag.startY) <
                    minimumVerticalDragDistance) {
                return;
              }

              event.preventDefault();
              const gestureId = activeDirectDrag.gestureId;
              stopDirectDrag(event, false);
              postDirectDragSignal('start', gestureId);
              return;
            }

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

          viewport.addEventListener('pointerup', event => {
            stopPan(event);
            stopDirectDrag(event);
            if (activeResizeGesture !== null &&
                event.pointerId === activeResizeGesture.pointerId) {
              rememberPointer(event);
              postVisualResizePointer(
                'up',
                activeResizeGesture,
                event);
              stopResizeGesture(event, false);
            }
            if (activeVisualGesture !== null &&
                event.pointerId === activeVisualGesture.pointerId) {
              rememberPointer(event);
              postVisualPointer(
                'up',
                activeVisualGesture,
                event);
              stopVisualGesture(event, false);
            }
          });
          viewport.addEventListener('pointercancel', event => {
            stopPan(event);
            stopDirectDrag(event);
            stopResizeGesture(event);
            stopVisualGesture(event);
          });
          viewport.addEventListener('lostpointercapture', event => {
            stopPan(event);
            stopDirectDrag(event);
            stopResizeGesture(event);
            stopVisualGesture(event);
          });
          viewport.addEventListener('pointerleave', event => {
            stopPan(event);
            stopDirectDrag(event);
            stopResizeGesture(event);
            stopVisualGesture(event);
          });
          viewport.addEventListener('scroll', scheduleViewportState);
          window.addEventListener('pointerup', event => {
            stopPan(event);
            stopDirectDrag(event);
            if (activeResizeGesture !== null &&
                event.pointerId === activeResizeGesture.pointerId) {
              rememberPointer(event);
              postVisualResizePointer(
                'up',
                activeResizeGesture,
                event);
              stopResizeGesture(event, false);
            }
            if (activeVisualGesture !== null &&
                event.pointerId === activeVisualGesture.pointerId) {
              rememberPointer(event);
              postVisualPointer(
                'up',
                activeVisualGesture,
                event);
              stopVisualGesture(event, false);
            }
          }, true);
          window.addEventListener('pointercancel', event => {
            stopPan(event);
            stopDirectDrag(event);
            stopResizeGesture(event);
            stopVisualGesture(event);
          }, true);
          viewport.addEventListener('dragstart', event => event.preventDefault());
          viewport.addEventListener('selectstart', event => event.preventDefault());
          viewport.addEventListener('auxclick', event => {
            if (event.button === 1) {
              event.preventDefault();
            }
          });
          viewport.addEventListener('contextmenu', event => {
            event.preventDefault();
            stopDirectDrag();
            stopResizeGesture();
            stopVisualGesture();
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
              stopResizeGesture();
              stopVisualGesture();
              refreshCursor();
            } else if (event.code === 'KeyH' &&
                       !event.ctrlKey && !event.altKey &&
                       !event.metaKey && !event.shiftKey &&
                       !event.repeat) {
              event.preventDefault();
              postPanCommand('toggle');
            } else if (event.code === 'KeyV' &&
                       !event.ctrlKey && !event.altKey &&
                       !event.metaKey && !event.shiftKey &&
                       !event.repeat) {
              event.preventDefault();
              stopResizeGesture();
              stopVisualGesture();
              postPanCommand('exit');
            } else if (!panModeEnabled &&
                       ['ArrowLeft', 'ArrowRight',
                        'ArrowUp', 'ArrowDown'].includes(event.code) &&
                       !event.ctrlKey && !event.altKey &&
                       !event.metaKey) {
              event.preventDefault();
              const step = event.shiftKey ? 10 : 1;
              postVisualNudge(
                event.code === 'ArrowLeft' ? -step :
                  event.code === 'ArrowRight' ? step : 0,
                event.code === 'ArrowUp' ? -step :
                  event.code === 'ArrowDown' ? step : 0);
            } else if (event.code === 'Escape') {
              event.preventDefault();
              stopDirectDrag();
              stopResizeGesture();
              stopVisualGesture();
              postPanCommand('exit');
            }
          });

          window.addEventListener('keyup', event => {
            if (event.code === 'Space') {
              spaceHeld = false;
              stopPan();
              stopDirectDrag();
              stopResizeGesture();
              refreshCursor();
            }
          });

          window.addEventListener('blur', () => {
            spaceHeld = false;
            stopPan();
            stopDirectDrag();
            stopResizeGesture();
            stopVisualGesture();
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
          const reportImageLoaded = () =>
            requestAnimationFrame(() => requestAnimationFrame(
              () => postImageState('loaded')));
          const reportImageError = () => postImageState('error');
          if (image.complete) {
            if (image.naturalWidth > 0 && image.naturalHeight > 0) {
              initializeViewport();
              reportImageLoaded();
            } else {
              reportImageError();
            }
          } else {
            image.addEventListener('load', initializeViewport, { once: true });
            image.addEventListener('load', reportImageLoaded, { once: true });
            image.addEventListener('error', reportImageError, { once: true });
          }
          new ResizeObserver(() => {
            refreshCursor();
            scheduleViewportState();
            positionResizeHandles();
          }).observe(viewport);
          document.body.dataset.hostScriptReady = 'true';
        })();
        """;

    public string Build(
        string validatedSvg,
        double renderedWidth,
        double renderedHeight,
        string bridgeToken,
        PreviewViewportPosition? initialViewport = null,
        long sourceRevision = 0,
        SvgVisualViewport? visualViewport = null)
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
        if (sourceRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRevision));
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
        SvgVisualViewport overlayViewport = visualViewport
            ?? new SvgVisualViewport(
                0,
                0,
                renderedWidth,
                renderedHeight,
                SvgPreserveAspectRatio.Default);
        string overlayViewBox = string.Join(
            " ",
            ToSafeCoordinate(overlayViewport.MinX),
            ToSafeCoordinate(overlayViewport.MinY),
            ToSafePositiveCoordinate(overlayViewport.Width),
            ToSafePositiveCoordinate(overlayViewport.Height));
        string overlayPreserveAspectRatio =
            WebUtility.HtmlEncode(
                overlayViewport.PreserveAspectRatio.SvgValue);
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
                .preview-viewport.select-mode {
                  cursor: default;
                }
                img.drag-ready {
                  cursor: default;
                }
                img.drag-armed {
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
                img,
                .selection-overlay {
                  display: block;
                  position: absolute;
                  left: 50%;
                  top: 50%;
                  transform: translate(-50%, -50%);
                  width: {{widthCss}}px;
                  height: {{heightCss}}px;
                  max-width: none;
                  max-height: none;
                }
                img {
                  pointer-events: auto;
                  user-select: none;
                }
                .selection-overlay {
                  pointer-events: none;
                  overflow: visible;
                  z-index: 1;
                }
                .selection-shape {
                  fill: transparent;
                  stroke: {{SelectionAccentColor}};
                  stroke-width: 1.5;
                  vector-effect: non-scaling-stroke;
                  transition: fill 90ms ease, stroke 90ms ease, stroke-width 90ms ease;
                }
                .preview-viewport.artwork-hovered
                    .selection-overlay[data-state="selected"]
                    .selection-shape {
                  fill: rgba(37, 99, 235, 0.055);
                  stroke: {{SelectionAccentStrongColor}};
                }
                .selection-overlay[data-state="moving"] .selection-shape {
                  fill: rgba(37, 99, 235, 0.09);
                  stroke: {{SelectionAccentStrongColor}};
                  stroke-width: 2;
                }
                .selection-overlay[data-state="resizing"] .selection-shape {
                  fill: rgba(37, 99, 235, 0.045);
                  stroke: {{SelectionAccentStrongColor}};
                  stroke-width: 2;
                }
                .resize-handle-layer {
                  position: absolute;
                  inset: 0;
                  pointer-events: none;
                  overflow: visible;
                  z-index: 2;
                }
                .resize-handle {
                  position: absolute;
                  width: {{ResizeHandleSizeCssPixels}}px;
                  height: {{ResizeHandleSizeCssPixels}}px;
                  box-sizing: border-box;
                  transform: translate(-50%, -50%);
                  border: 2px solid {{SelectionAccentColor}};
                  border-radius: 50%;
                  background: #ffffff;
                  box-shadow:
                    0 1px 3px rgba(15, 23, 42, 0.35),
                    0 0 0 1px rgba(255, 255, 255, 0.85);
                  pointer-events: auto;
                  transition: background 90ms ease, transform 90ms ease;
                }
                .resize-handle:hover,
                .resize-handle.active {
                  background: #dbeafe;
                  border-color: {{SelectionAccentStrongColor}};
                  transform: translate(-50%, -50%) scale(1.15);
                }
                .resize-handle[data-handle="top-left"],
                .resize-handle[data-handle="bottom-right"] {
                  cursor: nwse-resize;
                }
                .resize-handle[data-handle="top-right"],
                .resize-handle[data-handle="bottom-left"] {
                  cursor: nesw-resize;
                }
                .resize-handle[data-handle="top"],
                .resize-handle[data-handle="bottom"] {
                  cursor: ns-resize;
                }
                .resize-handle[data-handle="right"],
                .resize-handle[data-handle="left"] {
                  cursor: ew-resize;
                }
                .resize-handle[data-handle="start"],
                .resize-handle[data-handle="end"] {
                  cursor: move;
                }
              </style>
            </head>
            <body data-bridge-token="{{bridgeToken}}"
                  data-source-revision="{{sourceRevision}}"
                  data-initial-center-x="{{initialCenterX}}"
                  data-initial-center-y="{{initialCenterY}}">
              <div class="preview-viewport"
                   tabindex="0"
                   role="region"
                   aria-label="Live SVG preview">
                <main aria-label="SVG preview">
                  <img alt="Rendered SVG preview" draggable="false" src="data:image/svg+xml;base64,{{encodedSvg}}">
                  <svg class="selection-overlay"
                       aria-hidden="true"
                       viewBox="{{overlayViewBox}}"
                       preserveAspectRatio="{{overlayPreserveAspectRatio}}"></svg>
                  <div class="resize-handle-layer"
                       aria-hidden="true"></div>
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

    private static string ToSafeCoordinate(double value)
    {
        return (double.IsFinite(value) ? value : 0)
            .ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string ToSafePositiveCoordinate(double value)
    {
        return (double.IsFinite(value) && value > 0 ? value : 1)
            .ToString("0.######", CultureInfo.InvariantCulture);
    }
}
