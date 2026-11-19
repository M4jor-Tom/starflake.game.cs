{
  description = "Starflake game development shell";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

  outputs =
    { nixpkgs, ... }:
    {
      devShells =
        nixpkgs.lib.genAttrs
          [
            "x86_64-linux"
            "aarch64-linux"
          ]
          (
            system:
            let
              pkgs = nixpkgs.legacyPackages.${system};
            in
            {
              default = pkgs.mkShell {
                packages = [
                  pkgs.godot-mono
                  pkgs.dotnet-sdk_8
                  pkgs.uv
                  pkgs.python314
                ];
                # PyOxigraph's PyPI wheel (pulled in by linkml) needs the C++ runtime.
                shellHook = ''
                  export LD_LIBRARY_PATH="${pkgs.lib.makeLibraryPath [ pkgs.stdenv.cc.cc.lib ]}''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
                '';
                # uv's downloaded interpreters cannot run on NixOS: use the one above.
                UV_PYTHON_DOWNLOADS = "never";
                DOTNET_CLI_TELEMETRY_OPTOUT = "1";
                DOTNET_NOLOGO = "1";
              };
            }
          );
    };
}
