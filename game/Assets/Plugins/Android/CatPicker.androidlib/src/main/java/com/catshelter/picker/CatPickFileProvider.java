package com.catshelter.picker;

import androidx.core.content.FileProvider;

/**
 * A FileProvider of our own, declared in AndroidManifest.xml, existing only so
 * that androidx.core.content.FileProvider is not declared directly.
 *
 * Same reason CatShareFileProvider exists next door, and the androidx
 * reference says it plainly: "It is possible to use FileProvider directly
 * instead of extending it. However, this is not reliable and will causes
 * crashes on some devices."
 * (developer.android.com/reference/androidx/core/content/FileProvider, under
 * "Defining a FileProvider")
 *
 * A second provider rather than reusing CatShare's: the two serve different
 * directories for different reasons, and a share directory that the camera app
 * could also write into is a directory whose contents nobody owns.
 */
public class CatPickFileProvider extends FileProvider {

    public CatPickFileProvider() {
        super();
    }
}
