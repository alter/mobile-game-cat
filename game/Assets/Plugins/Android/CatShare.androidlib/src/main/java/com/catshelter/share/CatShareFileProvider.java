package com.catshelter.share;

import androidx.core.content.FileProvider;

/**
 * A FileProvider of our own, declared in AndroidManifest.xml, existing only so
 * that androidx.core.content.FileProvider is not declared directly.
 *
 * That is not decoration. The androidx reference says it plainly: "It is
 * possible to use FileProvider directly instead of extending it. However, this
 * is not reliable and will causes crashes on some devices."
 * (developer.android.com/reference/androidx/core/content/FileProvider, under
 * "Defining a FileProvider")
 *
 * The paths still come from the meta-data element in the manifest, not from
 * the constructor: androidx.core 1.9.0 added a FileProvider(int) constructor
 * that takes the XML resource, but the meta-data route works on every version
 * and keeps the provider and its allowed directories declared in the same
 * file.
 */
public class CatShareFileProvider extends FileProvider {

    public CatShareFileProvider() {
        super();
    }
}
