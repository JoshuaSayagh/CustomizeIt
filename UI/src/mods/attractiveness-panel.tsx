import { useValue, bindValue, trigger } from "cs2/api";
import { useState, useEffect, useCallback, useRef } from "react";
import styles from "./attractiveness-panel.module.scss";

// Bind to C# ValueBindings in BuildingAttractivenessUISystem
const hasBuilding$ = bindValue<boolean>("customizeIt", "hasBuilding", false);
const attractiveness$ = bindValue<number>("customizeIt", "attractiveness", 0);
const buildingName$ = bindValue<string>("customizeIt", "buildingName", "");
const hasOverride$ = bindValue<boolean>("customizeIt", "hasOverride", false);
const baseAttractiveness$ = bindValue<number>("customizeIt", "baseAttractiveness", 0);

const MIN_VALUE = 0;
const MAX_VALUE = 500;

// Custom slider using mouse events (Coherent Gameface lacks pointer event support)
const Slider = ({ value, onChange }: { value: number; onChange: (v: number) => void }) => {
    const trackRef = useRef<HTMLDivElement>(null);
    const [dragging, setDragging] = useState(false);
    const draggingRef = useRef(false);
    const onChangeRef = useRef(onChange);
    onChangeRef.current = onChange;

    const fraction = (value - MIN_VALUE) / (MAX_VALUE - MIN_VALUE);

    const getValueFromClientX = useCallback((clientX: number) => {
        const track = trackRef.current;
        if (!track) return MIN_VALUE;
        const rect = track.getBoundingClientRect();
        const ratio = Math.max(0, Math.min(1, (clientX - rect.left) / rect.width));
        return Math.round(MIN_VALUE + ratio * (MAX_VALUE - MIN_VALUE));
    }, []);

    // Attach document-level mousemove/mouseup so dragging works even outside the slider
    useEffect(() => {
        const onMouseMove = (e: MouseEvent) => {
            if (!draggingRef.current) return;
            e.stopPropagation();
            e.preventDefault();
            onChangeRef.current(getValueFromClientX(e.clientX));
        };

        const onMouseUp = (e: MouseEvent) => {
            if (!draggingRef.current) return;
            e.stopPropagation();
            draggingRef.current = false;
            setDragging(false);
        };

        document.addEventListener("mousemove", onMouseMove, true);
        document.addEventListener("mouseup", onMouseUp, true);
        return () => {
            document.removeEventListener("mousemove", onMouseMove, true);
            document.removeEventListener("mouseup", onMouseUp, true);
        };
    }, [getValueFromClientX]);

    const onMouseDown = useCallback((e: React.MouseEvent) => {
        e.stopPropagation();
        e.preventDefault();
        draggingRef.current = true;
        setDragging(true);
        onChangeRef.current(getValueFromClientX(e.clientX));
    }, [getValueFromClientX]);

    return (
        <div
            className={styles.sliderContainer}
            ref={trackRef}
            onMouseDown={onMouseDown}
        >
            <div className={styles.sliderTrack}>
                <div
                    className={styles.sliderFill}
                    style={{ width: `${fraction * 100}%` }}
                />
            </div>
            <div
                className={`${styles.sliderThumb}${dragging ? ` ${styles.sliderThumbActive}` : ""}`}
                style={{ left: `${fraction * 100}%` }}
            />
        </div>
    );
};

// Block all mouse/pointer events from reaching the game
const blockEvents = {
    onMouseDown: (e: React.MouseEvent) => e.stopPropagation(),
    onMouseUp: (e: React.MouseEvent) => e.stopPropagation(),
    onClick: (e: React.MouseEvent) => e.stopPropagation(),
    onPointerDown: (e: React.PointerEvent) => e.stopPropagation(),
    onPointerUp: (e: React.PointerEvent) => e.stopPropagation(),
    onDoubleClick: (e: React.MouseEvent) => e.stopPropagation(),
};

export const AttractivenessPanel = () => {
    const hasBuilding = useValue(hasBuilding$);
    const attractiveness = useValue(attractiveness$);
    const buildingName = useValue(buildingName$);
    const hasOverride = useValue(hasOverride$);
    const baseAttractiveness = useValue(baseAttractiveness$);

    const [sliderValue, setSliderValue] = useState(attractiveness);

    // Sync slider when the C# side pushes a new value
    useEffect(() => {
        setSliderValue(attractiveness);
    }, [attractiveness]);

    if (!hasBuilding) {
        return null;
    }

    const onApply = () => {
        trigger("customizeIt", "setAttractiveness", sliderValue);
    };

    const onReset = () => {
        setSliderValue(attractiveness);
    };

    const onRestoreDefault = () => {
        trigger("customizeIt", "restoreDefault");
    };

    const onClose = () => {
        trigger("customizeIt", "closePanel");
    };

    return (
        <div className={styles.panel} {...blockEvents}>
            <div className={styles.header}>
                <div className={styles.titleArea}>
                    <div className={styles.title}>Customize It</div>
                    <div className={styles.buildingName}>{buildingName}</div>
                </div>
                <button className={styles.closeButton} onClick={onClose}>
                    &#x2715;
                </button>
            </div>

            <div className={styles.body}>
                <div className={styles.row}>
                    <span className={styles.label}>Attractiveness</span>
                    <span className={styles.value}>{sliderValue}</span>
                </div>

                {hasOverride && (
                    <div className={styles.baseRow}>
                        Base: {baseAttractiveness}
                        <span className={styles.overrideTag}>MODIFIED</span>
                    </div>
                )}

                <Slider value={sliderValue} onChange={setSliderValue} />

                <div className={styles.buttonRow}>
                    <button className={styles.button} onClick={onReset}>
                        Reset
                    </button>
                    <button
                        className={`${styles.button} ${styles.applyButton}`}
                        onClick={onApply}
                    >
                        Apply
                    </button>
                </div>

                {hasOverride && (
                    <button
                        className={`${styles.button} ${styles.restoreButton}`}
                        onClick={onRestoreDefault}
                    >
                        Restore Default
                    </button>
                )}
            </div>
        </div>
    );
};
