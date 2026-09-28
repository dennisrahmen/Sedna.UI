/* cytoscape-bubblesets 4.1.0 — vendored into Sedna.UI by build/vendor-cytoscape.sh.
   Source: https://registry.npmjs.org/cytoscape-bubblesets/-/cytoscape-bubblesets-4.1.0.tgz (sha512 pinned and verified).
   Upstream's ES module, its bare imports pointed at the files beside it, with its source-map comment removed.
   Licence: MIT — see licenses/cytoscape-bubblesets.txt. */
/**
 * cytoscape-bubblesets
 * https://github.com/upsetjs/cytoscape.js-bubblesets
 *
 * Copyright (c) 2021-2022 Samuel Gratzl <sam@sgratzl.com>
 */

import { layers } from './cytoscape-layers.js';
import { Area, Line, calculatePotentialOutline, defaultOptions, createGenericInfluenceArea, createRectangleInfluenceArea, createLineInfluenceArea, calculateVirtualEdges, Circle, Rectangle } from './bubblesets.js';
import throttle from './lodash-throttle.js';

/******************************************************************************
Copyright (c) Microsoft Corporation.

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH
REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY
AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM
LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR
OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR
PERFORMANCE OF THIS SOFTWARE.
***************************************************************************** */
/* global Reflect, Promise, SuppressedError, Symbol, Iterator */


function __classPrivateFieldGet(receiver, state, kind, f) {
    if (kind === "a" && !f) throw new TypeError("Private accessor was defined without a getter");
    if (typeof state === "function" ? receiver !== state || !f : !state.has(receiver)) throw new TypeError("Cannot read private member from an object whose class did not declare it");
    return kind === "m" ? f : kind === "a" ? f.call(receiver) : f ? f.value : state.get(receiver);
}

function __classPrivateFieldSet(receiver, state, value, kind, f) {
    if (kind === "m") throw new TypeError("Private method is not writable");
    if (kind === "a" && !f) throw new TypeError("Private accessor was defined without a setter");
    if (typeof state === "function" ? receiver !== state || !f : !state.has(receiver)) throw new TypeError("Cannot write private member to an object whose class did not declare it");
    return (kind === "a" ? f.call(receiver, value) : f ? f.value = value : state.set(receiver, value)), value;
}

typeof SuppressedError === "function" ? SuppressedError : function (error, suppressed, message) {
    var e = new Error(message);
    return e.name = "SuppressedError", e.error = error, e.suppressed = suppressed, e;
};

var _BubbleSetPath_activeArea, _BubbleSetPath_potentialArea, _BubbleSetPath_options, _BubbleSetPath_virtualEdgeAreas, _BubbleSetPath_throttledUpdate, _BubbleSetPath_adder, _BubbleSetPath_remover, _BubbleSetPath_adapter;
function round2(v) {
    return Math.round(v * 100) / 100;
}
const SCRATCH_KEY = 'bubbleSets';
const circularBase = ['ellipse', 'diamond', 'diamond', 'pentagon', 'diamond', 'hexagon', 'heptagon', 'octagon', 'star'];
const circular = new Set(circularBase.concat(circularBase.map((v) => `round-${v}`)));
function isCircleShape(shape) {
    return circular.has(shape);
}
function toNodeKey(data) {
    return `${round2(data.shape.width)}x${round2(data.shape.height)}x${data.isCircle}`;
}
function toEdgeKey(line) {
    return `${round2(line.x1)}x${round2(line.y1)}x${round2(line.x2)}x${round2(line.y2)}`;
}
function linesEquals(a, b) {
    return a.length === b.length && a.every((ai, i) => toEdgeKey(ai) === toEdgeKey(b[i]));
}
function createShape(isCircle, bb) {
    return isCircle
        ? new Circle(bb.x1 + bb.w / 2, bb.y1 + bb.h / 2, Math.max(bb.w, bb.h) / 2)
        : new Rectangle(bb.x1, bb.y1, bb.w, bb.h);
}
class BubbleSetPath {
    constructor(adapter, node, nodes, edges, avoidNodes, options = {}) {
        this.node = node;
        this.nodes = nodes;
        this.edges = edges;
        this.avoidNodes = avoidNodes;
        _BubbleSetPath_activeArea.set(this, { x: 0, y: 0, width: 0, height: 0 });
        _BubbleSetPath_potentialArea.set(this, new Area(4, 0, 0, 0, 0, 0, 0));
        _BubbleSetPath_options.set(this, void 0);
        _BubbleSetPath_virtualEdgeAreas.set(this, new Map());
        _BubbleSetPath_throttledUpdate.set(this, void 0);
        _BubbleSetPath_adder.set(this, void 0);
        _BubbleSetPath_remover.set(this, void 0);
        _BubbleSetPath_adapter.set(this, void 0);
        this.update = (forceUpdate = false) => {
            const bb = this.nodes.union(this.edges).boundingBox(__classPrivateFieldGet(this, _BubbleSetPath_options, "f"));
            let potentialAreaDirty = false;
            const padding = Math.max(__classPrivateFieldGet(this, _BubbleSetPath_options, "f").edgeR1, __classPrivateFieldGet(this, _BubbleSetPath_options, "f").nodeR1) + __classPrivateFieldGet(this, _BubbleSetPath_options, "f").morphBuffer;
            const nextPotentialBB = {
                x: bb.x1 - padding,
                y: bb.y1 - padding,
                width: bb.w + padding * 2,
                height: bb.h + padding * 2,
            };
            if (forceUpdate || __classPrivateFieldGet(this, _BubbleSetPath_activeArea, "f").x !== nextPotentialBB.x || __classPrivateFieldGet(this, _BubbleSetPath_activeArea, "f").y !== nextPotentialBB.y) {
                potentialAreaDirty = true;
                __classPrivateFieldSet(this, _BubbleSetPath_potentialArea, Area.fromPixelRegion(nextPotentialBB, __classPrivateFieldGet(this, _BubbleSetPath_options, "f").pixelGroup), "f");
            }
            else if (__classPrivateFieldGet(this, _BubbleSetPath_activeArea, "f").width !== nextPotentialBB.width || __classPrivateFieldGet(this, _BubbleSetPath_activeArea, "f").height !== nextPotentialBB.height) {
                __classPrivateFieldSet(this, _BubbleSetPath_potentialArea, Area.fromPixelRegion(nextPotentialBB, __classPrivateFieldGet(this, _BubbleSetPath_options, "f").pixelGroup), "f");
            }
            __classPrivateFieldSet(this, _BubbleSetPath_activeArea, nextPotentialBB, "f");
            const potentialArea = __classPrivateFieldGet(this, _BubbleSetPath_potentialArea, "f");
            const cache = new Map();
            if (!potentialAreaDirty) {
                this.nodes.forEach((n) => {
                    var _a;
                    const data = ((_a = n.scratch(SCRATCH_KEY)) !== null && _a !== void 0 ? _a : null);
                    if (data && data.area) {
                        cache.set(toNodeKey(data), data.area);
                    }
                });
            }
            let updateEdges = false;
            const updateNodeData = (n) => {
                var _a;
                const nodeBB = n.boundingBox(__classPrivateFieldGet(this, _BubbleSetPath_options, "f"));
                let data = ((_a = n.scratch(SCRATCH_KEY)) !== null && _a !== void 0 ? _a : null);
                const isCircle = isCircleShape(n.style('shape'));
                if (!data ||
                    potentialAreaDirty ||
                    !data.area ||
                    data.isCircle !== isCircle ||
                    data.shape.width !== nodeBB.w ||
                    data.shape.height !== nodeBB.h) {
                    updateEdges = true;
                    data = {
                        isCircle,
                        shape: createShape(isCircle, nodeBB),
                    };
                    const key = toNodeKey(data);
                    const cached = cache.get(key);
                    if (cached != null) {
                        data.area = __classPrivateFieldGet(this, _BubbleSetPath_potentialArea, "f").copy(cached, {
                            x: nodeBB.x1 - __classPrivateFieldGet(this, _BubbleSetPath_options, "f").nodeR1,
                            y: nodeBB.y1 - __classPrivateFieldGet(this, _BubbleSetPath_options, "f").nodeR1,
                        });
                    }
                    else {
                        data.area = data.isCircle
                            ? createGenericInfluenceArea(data.shape, potentialArea, __classPrivateFieldGet(this, _BubbleSetPath_options, "f").nodeR1)
                            : createRectangleInfluenceArea(data.shape, potentialArea, __classPrivateFieldGet(this, _BubbleSetPath_options, "f").nodeR1);
                        cache.set(key, data.area);
                    }
                    n.scratch(SCRATCH_KEY, data);
                }
                else if (data.shape.x !== nodeBB.x1 || data.shape.y !== nodeBB.y1) {
                    updateEdges = true;
                    data.shape = createShape(isCircle, nodeBB);
                    data.area = __classPrivateFieldGet(this, _BubbleSetPath_potentialArea, "f").copy(data.area, {
                        x: nodeBB.x1 - __classPrivateFieldGet(this, _BubbleSetPath_options, "f").nodeR1,
                        y: nodeBB.y1 - __classPrivateFieldGet(this, _BubbleSetPath_options, "f").nodeR1,
                    });
                }
                return data;
            };
            const members = this.nodes.map(updateNodeData);
            const nonMembers = this.avoidNodes.map(updateNodeData);
            const edgeCache = new Map();
            if (!potentialAreaDirty) {
                __classPrivateFieldGet(this, _BubbleSetPath_virtualEdgeAreas, "f").forEach((value, key) => edgeCache.set(key, value));
                this.edges.forEach((n) => {
                    var _a;
                    const data = ((_a = n.scratch(SCRATCH_KEY)) !== null && _a !== void 0 ? _a : null);
                    if (data && data.lines) {
                        data.lines.forEach((line, i) => {
                            const area = data.areas[i];
                            if (area) {
                                cache.set(toEdgeKey(line), area);
                            }
                        });
                    }
                });
            }
            const updateEdgeArea = (line) => {
                const key = toEdgeKey(line);
                const cached = edgeCache.get(key);
                if (cached != null) {
                    return cached;
                }
                const r = createLineInfluenceArea(line, __classPrivateFieldGet(this, _BubbleSetPath_potentialArea, "f"), __classPrivateFieldGet(this, _BubbleSetPath_options, "f").edgeR1);
                edgeCache.set(key, r);
                return r;
            };
            const edges = [];
            this.edges.forEach((e) => {
                var _a, _b;
                const ps = ((_a = e.segmentPoints()) !== null && _a !== void 0 ? _a : [e.sourceEndpoint(), e.targetEndpoint()]).map((d) => ({ ...d }));
                if (ps.length === 0) {
                    return;
                }
                const lines = ps.slice(1).map((next, i) => {
                    const prev = ps[i];
                    return Line.from({
                        x1: prev.x,
                        y1: prev.y,
                        x2: next.x,
                        y2: next.y,
                    });
                });
                let data = ((_b = e.scratch(SCRATCH_KEY)) !== null && _b !== void 0 ? _b : null);
                if (!data || potentialAreaDirty || !linesEquals(data.lines, lines)) {
                    data = {
                        lines,
                        areas: lines.map(updateEdgeArea),
                    };
                    e.scratch(SCRATCH_KEY, data);
                }
                edges.push(...data.areas);
            });
            const memberShapes = members.map((d) => d.shape);
            if (__classPrivateFieldGet(this, _BubbleSetPath_options, "f").virtualEdges) {
                if (updateEdges) {
                    const nonMembersShapes = nonMembers.map((d) => d.shape);
                    const lines = calculateVirtualEdges(memberShapes, nonMembersShapes, __classPrivateFieldGet(this, _BubbleSetPath_options, "f").maxRoutingIterations, __classPrivateFieldGet(this, _BubbleSetPath_options, "f").morphBuffer);
                    __classPrivateFieldGet(this, _BubbleSetPath_virtualEdgeAreas, "f").clear();
                    lines.forEach((line) => {
                        const area = updateEdgeArea(line);
                        const key = toEdgeKey(line);
                        __classPrivateFieldGet(this, _BubbleSetPath_virtualEdgeAreas, "f").set(key, area);
                        edges.push(area);
                    });
                }
                else {
                    __classPrivateFieldGet(this, _BubbleSetPath_virtualEdgeAreas, "f").forEach((area) => edges.push(area));
                }
            }
            const memberAreas = members.filter((d) => d.area != null).map((d) => d.area);
            const nonMemberAreas = nonMembers.filter((d) => d.area != null).map((d) => d.area);
            const path = calculatePotentialOutline(potentialArea, memberAreas, edges, nonMemberAreas, (p) => p.containsElements(memberShapes), __classPrivateFieldGet(this, _BubbleSetPath_options, "f"));
            this.node.setAttribute('d', path.sample(8).simplify(0).bSplines().simplify(0).toString(2));
        };
        __classPrivateFieldSet(this, _BubbleSetPath_adapter, adapter, "f");
        __classPrivateFieldSet(this, _BubbleSetPath_options, {
            ...defaultOptions,
            style: {
                stroke: 'black',
                fill: 'black',
                fillOpacity: '0.25',
            },
            className: '',
            throttle: 100,
            virtualEdges: false,
            interactive: false,
            includeLabels: false,
            includeMainLabels: false,
            includeOverlays: false,
            includeSourceLabels: false,
            includeTargetLabels: false,
            ...options,
        }, "f");
        Object.assign(this.node.style, __classPrivateFieldGet(this, _BubbleSetPath_options, "f").style);
        if (__classPrivateFieldGet(this, _BubbleSetPath_options, "f").className) {
            this.node.classList.add(__classPrivateFieldGet(this, _BubbleSetPath_options, "f").className);
        }
        if (__classPrivateFieldGet(this, _BubbleSetPath_options, "f").interactive) {
            this.node.addEventListener('dblclick', () => {
                this.nodes.select();
            });
        }
        __classPrivateFieldSet(this, _BubbleSetPath_throttledUpdate, throttle(() => {
            this.update();
        }, __classPrivateFieldGet(this, _BubbleSetPath_options, "f").throttle), "f");
        __classPrivateFieldSet(this, _BubbleSetPath_adder, (e) => {
            e.target.on('add', __classPrivateFieldGet(this, _BubbleSetPath_adder, "f"));
            e.target.on('remove', __classPrivateFieldGet(this, _BubbleSetPath_remover, "f"));
            __classPrivateFieldGet(this, _BubbleSetPath_throttledUpdate, "f").call(this);
        }, "f");
        __classPrivateFieldSet(this, _BubbleSetPath_remover, (e) => {
            e.target.off('add', undefined, __classPrivateFieldGet(this, _BubbleSetPath_adder, "f"));
            e.target.off('remove', undefined, __classPrivateFieldGet(this, _BubbleSetPath_remover, "f"));
            __classPrivateFieldGet(this, _BubbleSetPath_throttledUpdate, "f").call(this);
        }, "f");
        nodes.on('position', __classPrivateFieldGet(this, _BubbleSetPath_throttledUpdate, "f"));
        nodes.on('add', __classPrivateFieldGet(this, _BubbleSetPath_adder, "f"));
        nodes.on('remove', __classPrivateFieldGet(this, _BubbleSetPath_remover, "f"));
        avoidNodes.on('position', __classPrivateFieldGet(this, _BubbleSetPath_throttledUpdate, "f"));
        avoidNodes.on('add', __classPrivateFieldGet(this, _BubbleSetPath_adder, "f"));
        avoidNodes.on('remove', __classPrivateFieldGet(this, _BubbleSetPath_remover, "f"));
        edges.on('move position', __classPrivateFieldGet(this, _BubbleSetPath_throttledUpdate, "f"));
        edges.on('add', __classPrivateFieldGet(this, _BubbleSetPath_adder, "f"));
        edges.on('remove', __classPrivateFieldGet(this, _BubbleSetPath_remover, "f"));
    }
    remove() {
        for (const set of [this.nodes, this.edges, this.avoidNodes]) {
            set.off('move position', undefined, __classPrivateFieldGet(this, _BubbleSetPath_throttledUpdate, "f"));
            set.off('add', undefined, __classPrivateFieldGet(this, _BubbleSetPath_adder, "f"));
            set.off('remove', undefined, __classPrivateFieldGet(this, _BubbleSetPath_remover, "f"));
            set.forEach((d) => {
                d.scratch(SCRATCH_KEY, {});
            });
        }
        this.node.remove();
        return __classPrivateFieldGet(this, _BubbleSetPath_adapter, "f").remove(this);
    }
}
_BubbleSetPath_activeArea = new WeakMap(), _BubbleSetPath_potentialArea = new WeakMap(), _BubbleSetPath_options = new WeakMap(), _BubbleSetPath_virtualEdgeAreas = new WeakMap(), _BubbleSetPath_throttledUpdate = new WeakMap(), _BubbleSetPath_adder = new WeakMap(), _BubbleSetPath_remover = new WeakMap(), _BubbleSetPath_adapter = new WeakMap();

var _BubbleSetsPlugin_layers, _BubbleSetsPlugin_adapter, _BubbleSetsPlugin_cy, _BubbleSetsPlugin_options;
const SVG_NAMESPACE = 'http://www.w3.org/2000/svg';
class BubbleSetsPlugin {
    constructor(currentCy, options = {}) {
        var _a;
        _BubbleSetsPlugin_layers.set(this, []);
        _BubbleSetsPlugin_adapter.set(this, {
            remove: (path) => {
                const index = __classPrivateFieldGet(this, _BubbleSetsPlugin_layers, "f").indexOf(path);
                if (index < 0) {
                    return false;
                }
                __classPrivateFieldGet(this, _BubbleSetsPlugin_layers, "f").splice(index, 1);
                return true;
            },
        });
        _BubbleSetsPlugin_cy.set(this, void 0);
        _BubbleSetsPlugin_options.set(this, void 0);
        __classPrivateFieldSet(this, _BubbleSetsPlugin_cy, currentCy, "f");
        __classPrivateFieldSet(this, _BubbleSetsPlugin_options, options, "f");
        this.layer = (_a = options.layer) !== null && _a !== void 0 ? _a : layers(currentCy).nodeLayer.insertBefore('svg');
    }
    destroy() {
        for (const path of __classPrivateFieldGet(this, _BubbleSetsPlugin_layers, "f")) {
            path.remove();
        }
        this.layer.remove();
    }
    addPath(nodes, edges = __classPrivateFieldGet(this, _BubbleSetsPlugin_cy, "f").collection(), avoidNodes = __classPrivateFieldGet(this, _BubbleSetsPlugin_cy, "f").collection(), options = {}) {
        const node = this.layer.node.ownerDocument.createElementNS(SVG_NAMESPACE, 'path');
        this.layer.node.appendChild(node);
        const path = new BubbleSetPath(__classPrivateFieldGet(this, _BubbleSetsPlugin_adapter, "f"), node, nodes, edges !== null && edges !== void 0 ? edges : __classPrivateFieldGet(this, _BubbleSetsPlugin_cy, "f").collection(), avoidNodes !== null && avoidNodes !== void 0 ? avoidNodes : __classPrivateFieldGet(this, _BubbleSetsPlugin_cy, "f").collection(), { ...__classPrivateFieldGet(this, _BubbleSetsPlugin_options, "f"), ...options });
        __classPrivateFieldGet(this, _BubbleSetsPlugin_layers, "f").push(path);
        path.update();
        return path;
    }
    getPaths() {
        return __classPrivateFieldGet(this, _BubbleSetsPlugin_layers, "f").slice();
    }
    removePath(path) {
        const i = __classPrivateFieldGet(this, _BubbleSetsPlugin_layers, "f").indexOf(path);
        if (i < 0) {
            return false;
        }
        return path.remove();
    }
    update(forceUpdate = false) {
        __classPrivateFieldGet(this, _BubbleSetsPlugin_layers, "f").forEach((p) => p.update(forceUpdate));
    }
}
_BubbleSetsPlugin_layers = new WeakMap(), _BubbleSetsPlugin_adapter = new WeakMap(), _BubbleSetsPlugin_cy = new WeakMap(), _BubbleSetsPlugin_options = new WeakMap();
function bubbleSets(options = {}) {
    return new BubbleSetsPlugin(this, options);
}

function register(cytoscape) {
    cytoscape('core', 'bubbleSets', bubbleSets);
}
function hasCytoscape(obj) {
    return typeof obj.cytoscape === 'function';
}
if (hasCytoscape(window)) {
    register(window.cytoscape);
}

export { BubbleSetPath, BubbleSetsPlugin, bubbleSets, register as default };
