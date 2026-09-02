/**
 * Проверка intoFileSpace перебором.
 *
 * Прямое преобразование взято из Decode.applyOrientation — то самое, что
 * Android делает матрицей: повернуть, отразить, и сдвинуть результат в
 * положительную четверть. Обратное — копия CatPhoto.point.
 *
 * Проверяется одно: для каждого пикселя файла прямое преобразование даёт
 * точку выпрямленного снимка, а обратное возвращает ровно тот же пиксель.
 * Плюс отдельно — что рамка целиком, поданная в обратное преобразование,
 * снова становится рамкой целиком, потому что именно на этом случае
 * устройство ничего не проверило.
 */
public class RotCheck {

    // прямое: пиксель файла (rw x rh) -> пиксель выпрямленного снимка
    static int[] forward(int x, int y, int exif, int rw, int rh) {
        switch (exif) {
            case 2:  return new int[] { rw - 1 - x, y };
            case 3:  return new int[] { rw - 1 - x, rh - 1 - y };
            case 4:  return new int[] { x, rh - 1 - y };
            case 5:  return new int[] { y, x };
            case 6:  return new int[] { rh - 1 - y, x };
            case 7:  return new int[] { rh - 1 - y, rw - 1 - x };
            case 8:  return new int[] { y, rw - 1 - x };
            default: return new int[] { x, y };
        }
    }

    // обратное: копия CatPhoto.point, width/height — размеры ВЫПРЯМЛЕННОГО
    static int[] back(int x, int y, int exif, int width, int height) {
        switch (exif) {
            case 2:  return new int[] { width - 1 - x, y };
            case 3:  return new int[] { width - 1 - x, height - 1 - y };
            case 4:  return new int[] { x, height - 1 - y };
            case 5:  return new int[] { y, x };
            case 6:  return new int[] { y, width - 1 - x };
            case 7:  return new int[] { height - 1 - y, width - 1 - x };
            case 8:  return new int[] { height - 1 - y, x };
            default: return new int[] { x, y };
        }
    }

    public static void main(String[] args) {
        int bad = 0, checked = 0;
        // несимметричные размеры, чтобы перепутанные оси не спрятались
        int[][] sizes = { {3000, 4000}, {4000, 3000}, {7, 13}, {13, 7}, {1, 9} };
        for (int[] s : sizes) {
            int rw = s[0], rh = s[1];
            for (int exif = 1; exif <= 8; exif++) {
                boolean sideways = exif >= 5;
                int W = sideways ? rh : rw;
                int H = sideways ? rw : rh;
                for (int y = 0; y < rh; y += Math.max(1, rh / 37)) {
                    for (int x = 0; x < rw; x += Math.max(1, rw / 37)) {
                        int[] up = forward(x, y, exif, rw, rh);
                        if (up[0] < 0 || up[0] >= W || up[1] < 0 || up[1] >= H) {
                            System.out.println("ВЫШЛИ ЗА КРАЙ exif=" + exif
                                    + " " + rw + "x" + rh + " точка " + x + "," + y);
                            bad++;
                            continue;
                        }
                        int[] again = back(up[0], up[1], exif, W, H);
                        checked++;
                        if (again[0] != x || again[1] != y) {
                            System.out.println("НЕ СОШЛОСЬ exif=" + exif + " " + rw + "x" + rh
                                    + ": " + x + "," + y + " -> " + up[0] + "," + up[1]
                                    + " -> " + again[0] + "," + again[1]);
                            bad++;
                        }
                    }
                }
            }
        }
        System.out.println("точек проверено: " + checked + ", расхождений: " + bad);

        // Рамка во весь кадр остаётся рамкой во весь кадр — тот самый случай,
        // на котором прогон на устройстве ничего не доказал.
        for (int exif = 1; exif <= 8; exif++) {
            boolean sideways = exif >= 5;
            int rw = 3000, rh = 4000;
            int W = sideways ? rh : rw, H = sideways ? rw : rh;
            int[] a = back(0, 0, exif, W, H);
            int[] b = back(W - 1, H - 1, exif, W, H);
            int left = Math.min(a[0], b[0]), top = Math.min(a[1], b[1]);
            int right = Math.max(a[0], b[0]) + 1, bottom = Math.max(a[1], b[1]) + 1;
            boolean whole = left == 0 && top == 0 && right == rw && bottom == rh;
            System.out.println("exif " + exif + ": весь кадр -> " + left + "," + top
                    + " " + (right - left) + "x" + (bottom - top)
                    + (whole ? "  ок" : "  ОШИБКА"));
            if (!whole) bad++;
        }
        System.out.println(bad == 0 ? "ВСЁ СОШЛОСЬ" : "ЕСТЬ ОШИБКИ: " + bad);
        if (bad != 0) System.exit(1);
    }
}
