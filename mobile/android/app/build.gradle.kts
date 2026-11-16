import java.util.Properties

plugins {
    id("com.android.application")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

// Release signing (ADR-0021 G3). The key never lives in the repo: it comes from android/key.properties
// (git-ignored) or, in CI, from the NIRENGI_* environment variables. Without it a release build fails
// instead of falling back to the debug key (see the task graph check below). docs/yerel-calistirma.md.
val keyProperties = Properties().apply {
    val file = rootProject.file("key.properties")
    if (file.exists()) {
        file.inputStream().use { load(it) }
    }
}

fun releaseSigningValue(property: String, env: String): String? =
    (keyProperties.getProperty(property) ?: System.getenv(env))?.takeIf { it.isNotBlank() }

val releaseStoreFile = releaseSigningValue("storeFile", "NIRENGI_KEYSTORE_FILE")
val releaseStorePassword = releaseSigningValue("storePassword", "NIRENGI_KEYSTORE_PASSWORD")
val releaseKeyAlias = releaseSigningValue("keyAlias", "NIRENGI_KEY_ALIAS")
val releaseKeyPassword = releaseSigningValue("keyPassword", "NIRENGI_KEY_PASSWORD")
val releaseSigningConfigured = listOf(
    releaseStoreFile,
    releaseStorePassword,
    releaseKeyAlias,
    releaseKeyPassword,
).all { it != null }

android {
    namespace = "app.nirengi.nirengi"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
        // flutter_local_notifications needs java.time on older Android versions.
        isCoreLibraryDesugaringEnabled = true
    }

    defaultConfig {
        // TODO: Specify your own unique Application ID (https://developer.android.com/studio/build/application-id.html).
        applicationId = "app.nirengi.nirengi"
        // You can update the following values to match your application needs.
        // For more information, see: https://flutter.dev/to/review-gradle-config.
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        // Uses the version code from pubspec.yaml. When using split APKs, 1000 * ABI_VERSION
        // is added automatically by Flutter. (https://developer.android.com/studio/build/configure-apk-splits#configure-APK-versions)
        // You can force using the value of versionCode by specifying the `-P force-version-code-ignoring-abi=true`
        // flag during build.
        versionCode = flutter.versionCode
        versionName = flutter.versionName
    }

    signingConfigs {
        // Shared, non-secret debug key committed to the repo (see .gitignore). Every machine and CI run signs
        // debug APKs with it, so a newer preview APK installs over the old one without uninstalling.
        getByName("debug") {
            storeFile = file("preview-debug.keystore")
            storePassword = "android"
            keyAlias = "androiddebugkey"
            keyPassword = "android"
        }
        if (releaseSigningConfigured) {
            create("release") {
                storeFile = rootProject.file(releaseStoreFile!!)
                storePassword = releaseStorePassword
                keyAlias = releaseKeyAlias
                keyPassword = releaseKeyPassword
            }
        }
    }

    buildTypes {
        release {
            // Never the debug key: unsigned unless the release key is given (and then the build is refused).
            signingConfig = if (releaseSigningConfigured) signingConfigs.getByName("release") else null
        }
    }
}

gradle.taskGraph.whenReady {
    val releaseBuild = allTasks.any {
        it.path.startsWith(":app:") && Regex("^(assemble|bundle|package).*Release$").matches(it.name)
    }
    if (releaseBuild && !releaseSigningConfigured) {
        throw GradleException(
            "Release signing is not configured. Provide android/key.properties (storeFile, storePassword, " +
                "keyAlias, keyPassword) or the environment variables NIRENGI_KEYSTORE_FILE, " +
                "NIRENGI_KEYSTORE_PASSWORD, NIRENGI_KEY_ALIAS, NIRENGI_KEY_PASSWORD. " +
                "Release builds are never signed with the debug key; see docs/yerel-calistirma.md.",
        )
    }
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

flutter {
    source = "../.."
}

dependencies {
    coreLibraryDesugaring("com.android.tools:desugar_jdk_libs:2.1.4")
}
