const path = require('path');
const webpack = require('webpack');

const VueLoaderPlugin = require('vue-loader/lib/plugin');
const CleanWebpackPlugin = require('clean-webpack-plugin');
//const BundleAnalyzerPlugin = require('webpack-bundle-analyzer').BundleAnalyzerPlugin;

let cleanOptions = { root: __dirname };

const ASSET_PATH = process.env.ASSET_PATH || '/dist/';  // If this isn't an absolute path (starts with /), hot module reload won't work if the app is reloaded in the browser from a sub directory.  Hot module reload is strictly a development feature and should / would be disabled for production builds.

module.exports =
	{
		mode: "development", // TODO: Change this to "production"
		//stats: { modules: false },
		devtool: 'source-map',
		entry: "./www/main.js",
		plugins: [
			new webpack.DefinePlugin({ 'process.BROWSER': true }),
			//new webpack.NamedModulesPlugin(),
			new CleanWebpackPlugin(['www/dist'], cleanOptions),
			new webpack.DefinePlugin({ 'process.env.ASSET_PATH': JSON.stringify(ASSET_PATH) }),
			new VueLoaderPlugin()
			//, new BundleAnalyzerPlugin()
		],
		output: {
			path: path.resolve(__dirname, "www/dist"),
			filename: 'bundle.js',
			publicPath: '/dist/'
		},
		module: {
			rules: [
				{
					test: /\.js$/,
					loader: 'babel-loader',
					include: __dirname,
					exclude: file => (
						/node_modules/.test(file) &&
						!/\.vue\.js/.test(file)
					)
				},
				{
					test: /\.css$/,
					loader: "style-loader!css-loader"
				},
				{
					test: /\.(gif|png|jpe?g|svg)$/i,
					include: path.resolve(__dirname, "www/images"),
					exclude: path.resolve(__dirname, "www/images/sprite"),
					use: [
						{
							loader: 'url-loader',
							options: {
								limit: 8196, // Convert images < 8kb to base64 strings
								name: 'images/[hash]-[name].[ext]'
							}
						},
						{
							loader: 'image-webpack-loader'
						}
					]
				},
				{
					test: /\.svg$/i,
					include: path.resolve(__dirname, "www/images/sprite"),
					use: [
						{
							loader: 'svg-sprite-loader',
							options:
								{
									esModule: false
								}
						},
						{
							loader: 'image-webpack-loader'
						}
					]
				},
				{
					test: /\.vue$/,
					loader: 'vue-loader',
					options: {
						hotReload: true
					}
				}
			]
		},
		resolve: {
			alias: {
				appRoot: path.resolve(__dirname, "www/")
			},
			"aliasFields": ["browser"]
		},
		devServer: {
			contentBase: path.join(__dirname, "www/dist"),
			compress: true,
			disableHostCheck: true,
			port: 9000,
			hot: true,
			host: "0.0.0.0",
			https: false,
			publicPath: "/dist/"
		}
	};